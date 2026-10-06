import { describe, expect, it } from 'vitest';
import topology from '../../../content/classic-topology.json';
import { map } from './map';

function contains(points: number[][], x: number, z: number) {
  let inside = false;
  for (let i = 0, j = points.length - 1; i < points.length; j = i++) {
    const a = points[i], b = points[j];
    if ((a[1] > z) !== (b[1] > z) && x < (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
  }
  return inside;
}

describe('Classic display cartography', () => {
  it('preserves territory identities, continents, and legal borders', () => {
    expect(map.territories.map(({ id, key, name, continent, neighbors }) => ({ id, key, name, continent, neighbors }))).toEqual(topology.territories);
    expect(map.continents.map(({ id, bonus }) => ({ id, bonus }))).toEqual(topology.continents.map(({ id, bonus }) => ({ id, bonus })));
  });
  it('places every troop marker inside its main land polygon', () => {
    for (const territory of map.territories) expect(contains(territory.shape, territory.x, territory.z), territory.name).toBe(true);
  });
  it('includes closed land contours and separate island polygons without invalid coordinates', () => {
    expect(map.territories.flatMap(t => t.parts ?? []).length).toBeGreaterThan(42);
    for (const territory of map.territories) {
      for (const polygon of territory.parts ?? []) {
        expect(polygon.length, territory.name).toBeGreaterThanOrEqual(3);
        expect(polygon.every(point => point.length === 2 && point.every(Number.isFinite)), territory.name).toBe(true);
      }
    }
  });
});
