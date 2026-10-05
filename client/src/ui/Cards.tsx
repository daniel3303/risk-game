import { useEffect, useState } from 'react';
import { Layers, X } from 'lucide-react';
import { map } from '../game/map';
import { cardBonus } from '../game/moves';
import type { GameView, Snapshot } from '../game/types';
import { session } from '../net/session';

const symbols = { infantry: '♟', cavalry: '♞', artillery: '♜', wild: '✦' };

export function Cards({ room, game, seat, disabled, onClose }: { room: Snapshot; game: GameView; seat: number; disabled: boolean; onClose: () => void }) {
  const [selected, setSelected] = useState<number[]>([]);
  const [territory, setTerritory] = useState(-1);
  useEffect(() => { setSelected([]); setTerritory(-1); }, [game.hand]);
  const chosen = game.hand.filter(c => selected.includes(c.id));
  const bonus = cardBonus(chosen, room.options.cards, game.trades);
  const owned = chosen.filter(c => c.territory >= 0 && game.territories[c.territory].owner === seat);
  const canTrade = game.phase === 'draft' && game.currentPlayer === seat;
  return <section className="cards-panel" aria-label="Your territory cards"><div className="panel-title"><h2><Layers size={18} /> Your territory cards</h2><button className="icon-button" aria-label="Close cards" onClick={onClose}><X size={18} /></button></div>
    <p className="muted tiny">Trade three matching symbols, one of each, or a valid set with a wild. One card is earned for any successful turn of conquest.</p>
    {game.hand.length === 0 && <div className="empty-cards">Conquer a territory, then finish your turn to earn your first card.</div>}
    <div className="card-hand">{game.hand.map(card => <button key={card.id} className={`territory-card ${selected.includes(card.id) ? 'chosen' : ''}`} aria-pressed={selected.includes(card.id)} onClick={() => setSelected(ids => ids.includes(card.id) ? ids.filter(id => id !== card.id) : ids.length < 3 ? [...ids, card.id] : ids)}><span className="card-symbol">{symbols[card.symbol]}</span><strong>{card.territory >= 0 ? map.territories[card.territory].name : 'Wild card'}</strong><small>{card.symbol}</small></button>)}</div>
    {game.forcedTrade && canTrade && <p className="gold-text">You must trade a set before drafting.</p>}
    {owned.length > 0 && <label>Place the +2 territory bonus<select value={territory} onChange={e => setTerritory(Number(e.target.value))}><option value={-1}>First eligible territory</option>{owned.map(card => <option key={card.id} value={card.territory}>{map.territories[card.territory].name}</option>)}</select></label>}
    <button className="primary full" disabled={disabled || !canTrade || bonus === 0} onClick={() => void session.act({ kind: 'trade', cards: selected, bonusTerritory: territory })}>{bonus > 0 ? `Trade for ${bonus} troops` : 'Select a valid set of three'}</button>
    {!canTrade && <p className="muted tiny">Cards can be traded during your Draft phase.</p>}
  </section>;
}
