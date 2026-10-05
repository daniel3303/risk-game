import { describe, it, expect } from 'vitest';
import { acceptsSnapshot, cardBonus, connected, targets } from './moves';
import { map } from './map';
import type { Card, GameView, Territory } from './types';

describe('legal move guidance', () => {
  const board = (): Territory[] => map.territories.map(t => ({ id: t.id, owner: 1, troops: 1 }));
  it('permits fortification through a chain of owned territories', () => {
    const territories = board();
    [0, 3, 6].forEach(id => territories[id].owner = 0);
    expect(connected(territories, 0, 6, 0)).toBe(true);
    territories[3].owner = 1;
    expect(connected(territories, 0, 6, 0)).toBe(false);
  });
  it('only offers adjacent enemies during the acting players attack phase', () => {
    const territories = board();
    territories[0] = { id: 0, owner: 0, troops: 4 };
    const game = { territories, phase: 'attack', currentPlayer: 0 } as GameView;
    expect(targets(game, 0, 0)).toEqual([1, 3, 29]);
    expect(targets(game, 0, 1)).toEqual([]);
  });
  it('rejects stale revisions while allowing an initial snapshot', () => {
    expect(acceptsSnapshot({ code: 'ABCDEF', revision: 12 }, { code: 'ABCDEF', revision: 11 })).toBe(false);
    expect(acceptsSnapshot(null, { code: 'ABCDEF', revision: 1 })).toBe(true);
  });
});

describe('card previews', () => {
  const card = (id: number, symbol: Card['symbol']): Card => ({ id, symbol, territory: id });
  it('scores a mixed-symbol set with a wild as ten fixed troops', () => {
    expect(cardBonus([card(0, 'infantry'), card(1, 'cavalry'), card(42, 'wild')], 'fixed', 0)).toBe(10);
  });
  it('rejects a partial mixed set without a wild', () => {
    expect(cardBonus([card(0, 'infantry'), card(3, 'infantry'), card(1, 'cavalry')], 'fixed', 0)).toBe(0);
  });
  it('uses the global progressive trade schedule', () => {
    expect(cardBonus([card(0, 'infantry'), card(3, 'infantry'), card(6, 'infantry')], 'progressive', 7)).toBe(25);
  });
});
