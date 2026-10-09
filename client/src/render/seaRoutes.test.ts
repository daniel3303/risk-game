import { describe, expect, it } from 'vitest';
import { map } from '../game/map';
import { coastGap, seaRoutes } from './seaRoutes';

// Outlines that come closer than this share a land border; farther neighbours are only reachable by sea and need a route.
const touching = 0.6;

describe('sea routes', () => {
  const neighbours = map.territories.flatMap(t => t.neighbors.filter(n => n > t.id).map(n => [t.id, n] as [number, number]));
  const key = ([a, b]: [number, number]) => `${Math.min(a, b)}-${Math.max(a, b)}`;

  it('draws a route for every pair of neighbours separated by water', () => {
    const separated = neighbours.filter(([a, b]) => coastGap(a, b) > touching).map(key).sort();
    const drawn = seaRoutes.map(key).sort();
    expect(separated.filter(pair => !drawn.includes(pair))).toEqual([]);
  });

  it('only connects territories that border each other', () => {
    const legal = new Set(neighbours.map(key));
    expect(seaRoutes.map(key).filter(pair => !legal.has(pair))).toEqual([]);
    expect(new Set(seaRoutes.map(key)).size).toBe(seaRoutes.length);
  });
});
