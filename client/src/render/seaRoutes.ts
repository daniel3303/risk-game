import { map } from '../game/map';

/** Neighbouring territories whose coastlines do not touch; each pair is drawn as a dotted sea route with a port at either shore. */
export const seaRoutes: [number, number][] = [
  [0, 29], [1, 2], [2, 4], [2, 5], [2, 13], [13, 14], [13, 16], [14, 16], [16, 17], [16, 18], [19, 21], [19, 35], [18, 20], [11, 20],
  [22, 35], [24, 25], [22, 25], [31, 32], [29, 32], [37, 38], [38, 39], [38, 40], [39, 40], [39, 41],
];

/** Shortest distance between the two territories' outlines in map units. */
export function coastGap(a: number, b: number) {
  let best = Infinity;
  for (const [x1, z1] of map.territories[a].shape) for (const [x2, z2] of map.territories[b].shape) best = Math.min(best, Math.hypot(x1 - x2, z1 - z2));
  return best;
}
