import { readFile, writeFile } from 'node:fs/promises';
import { svgPathProperties } from 'svg-path-properties';
import polylabel from 'polylabel';

const content = new URL('../../content/', import.meta.url);
const topology = JSON.parse(await readFile(new URL('classic-topology.json', content), 'utf8'));
const source = JSON.parse(await readFile(new URL('cartography.json', content), 'utf8'));
const round = value => Math.round(value * 1000) / 1000;
const area = points => Math.abs(points.reduce((sum, p, i) => {
  const q = points[(i + 1) % points.length];
  return sum + p[0] * q[1] - q[0] * p[1];
}, 0)) / 2;

function polygons(path) {
  const offset = path.transform?.match(/translate\((-?[\d.]+),\s*(-?[\d.]+)\)/);
  const dx = offset ? Number(offset[1]) : 0;
  const dy = offset ? Number(offset[2]) : 0;
  return path.d.match(/M[^M]+/g).map(subpath => {
    const curve = new svgPathProperties(subpath);
    const points = [];
    for (const part of curve.getParts()) {
      const steps = ['C', 'Q', 'A'].includes(part.details?.[0]) ? Math.max(1, Math.ceil(part.length / 2.5)) : 1;
      for (let i = 0; i < steps; i++) {
        const point = part.getPointAtLength(part.length * i / steps);
        const next = [round((point.x + dx - 542) / 16), round((316.5 - point.y - dy) / 16 * 0.75 + 2.7)];
        if (!points.length || Math.hypot(next[0] - points.at(-1)[0], next[1] - points.at(-1)[1]) > 0.012) points.push(next);
      }
    }
    return points;
  }).filter(points => points.length >= 3 && area(points) > 0.002).sort((a, b) => area(b) - area(a));
}

for (const territory of topology.territories) {
  const key = territory.key === 'russia' ? 'ukraine' : territory.key.replaceAll('-', '_');
  const path = source.paths[key];
  if (!path) throw new Error(`Missing cartography for ${territory.name}`);
  const parts = polygons(path);
  if (!parts.length) throw new Error(`Empty territory ${territory.name}`);
  const center = polylabel([parts[0]], 0.025);
  Object.assign(territory, { x: round(center[0]), z: round(center[1]), shape: parts[0], parts });
}
topology.cartography = { source: source.source, authors: source.authors, license: source.license, licenseUrl: source.licenseUrl };
// Keep coordinate pairs on one line so generated contours are easy to review.
const json = JSON.stringify(topology, null, 2).replace(/\[\n\s+(-?[\d.]+),\n\s+(-?[\d.]+)\n\s+\]/g, '[$1, $2]');
await writeFile(new URL('classic.json', content), json + '\n');
console.log(`Generated ${topology.territories.length} detailed territories with ${topology.territories.reduce((sum, t) => sum + t.parts.length, 0)} land polygons.`);
