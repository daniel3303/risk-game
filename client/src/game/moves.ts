import { map } from './map';
import type { Card, GameView, Territory } from './types';

export function connected(territories: Territory[], from: number, to: number, owner: number): boolean {
  if (territories[from]?.owner !== owner || territories[to]?.owner !== owner) return false;
  const seen = new Set([from]);
  const queue = [from];
  for (let i = 0; i < queue.length; i++) {
    if (queue[i] === to) return true;
    for (const neighbor of map.territories[queue[i]].neighbors) {
      if (territories[neighbor].owner === owner && !seen.has(neighbor)) { seen.add(neighbor); queue.push(neighbor); }
    }
  }
  return false;
}

export function targets(game: GameView | null, selected: number | null, seat: number): number[] {
  if (!game || selected === null || game.currentPlayer !== seat) return [];
  const from = game.territories[selected];
  if (from.owner !== seat || from.troops < 2) return [];
  if (game.phase === 'attack') return map.territories[selected].neighbors.filter(id => game.territories[id].owner !== seat);
  if (game.phase === 'fortify') return game.territories.filter(t => t.id !== selected && connected(game.territories, selected, t.id, seat)).map(t => t.id);
  return [];
}

export function cardBonus(cards: Card[], mode: 'fixed' | 'progressive', trades: number): number {
  if (cards.length !== 3) return 0;
  const symbols = cards.filter(c => c.symbol !== 'wild').map(c => c.symbol);
  const distinct = new Set(symbols).size;
  let bonus = 0;
  if (distinct === symbols.length) bonus = 10;
  else if (distinct === 1) bonus = { infantry: 4, cavalry: 6, artillery: 8, wild: 10 }[symbols[0]];
  if (!bonus || mode === 'fixed') return bonus;
  return trades < 5 ? 4 + trades * 2 : 15 + (trades - 5) * 5;
}

export function acceptsSnapshot(current: { code: string; revision: number } | null, incoming: { code: string; revision: number }): boolean {
  return !current || current.code !== incoming.code || incoming.revision >= current.revision;
}
