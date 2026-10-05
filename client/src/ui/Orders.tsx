import { useEffect, useState } from 'react';
import { ArrowRight, Flag, Shield, Swords } from 'lucide-react';
import { map } from '../game/map';
import { session } from '../net/session';
import type { GameView } from '../game/types';

export function Orders({ game, seat, selected, target, reachable, setTarget, disabled }: { game: GameView; seat: number; selected: number | null; target: number | null; reachable: number[]; setTarget: (id: number) => void; disabled: boolean }) {
  const [count, setCount] = useState(1);
  const [blitz, setBlitz] = useState(true);
  const [dice, setDice] = useState(3);
  const source = selected === null ? null : game.territories[selected];
  const owns = source?.owner === seat;
  const myTurn = game.currentPlayer === seat;
  const maximum = game.phase === 'draft' ? game.reinforcements : game.phase === 'occupy' ? game.capture?.maximum ?? 1 : game.phase === 'setup' ? 1 : Math.max(1, (source?.troops ?? 2) - 1);
  const minimum = game.phase === 'occupy' ? game.capture?.minimum ?? 1 : 1;
  useEffect(() => { setCount(maximum); setDice(Math.min(3, maximum)); }, [selected, game.phase, maximum]);
  const numberInput = <label className="troop-input">Troops<div><button className="icon-button" aria-label="Fewer troops" disabled={disabled || count <= minimum} onClick={() => setCount(v => Math.max(minimum, v - 1))}>−</button><input aria-label="Troop count" type="number" min={minimum} max={maximum} value={count} onChange={e => setCount(Math.max(minimum, Math.min(maximum, Number(e.target.value) || minimum)))} /><button className="icon-button" aria-label="More troops" disabled={disabled || count >= maximum} onClick={() => setCount(v => Math.min(maximum, v + 1))}>+</button></div></label>;
  if (!myTurn) return <div className="orders waiting-orders"><Shield size={27} /><strong>Watch the frontier.</strong><p className="muted">Another commander is making their move. Your territories and cards stay visible.</p></div>;
  if (game.phase === 'finished') return null;
  return <section className="orders" aria-label="Turn orders">
    {game.phase === 'claim' && <><p className="muted">Select an unclaimed territory to make it yours.</p><button className="primary full" disabled={disabled || !source || source.owner !== -1} onClick={() => void session.act({ kind: 'claim', to: selected! })}><Flag size={16} /> Claim territory</button></>}
    {(game.phase === 'draft' || game.phase === 'setup') && <><p className="muted">Select one of your territories, then place {game.phase === 'setup' ? 'one starting troop' : 'your reinforcements'}.</p>{owns && numberInput}<button className="primary full" disabled={disabled || !owns || game.forcedTrade} onClick={() => void session.act({ kind: 'place', to: selected!, count })}>Deploy {owns ? count : ''} troops<ArrowRight size={16} /></button></>}
    {game.phase === 'occupy' && game.capture && <><h3>{map.territories[game.capture.to].name} captured</h3><p className="muted">Move {minimum}–{maximum} troops from {map.territories[game.capture.from].name}. One must stay behind.</p>{numberInput}<button className="primary full" disabled={disabled} onClick={() => void session.act({ kind: 'occupy', count })}>Move into territory<Flag size={16} /></button></>}
    {(game.phase === 'attack' || game.phase === 'fortify') && <>
      <p className="muted">{game.phase === 'attack' ? 'Select your army, then an adjacent enemy.' : 'Move one army through your connected territories, or finish your turn.'}</p>
      {source && owns && source.troops > 1 && <><label>Destination<select aria-label="Destination" value={target ?? ''} onChange={e => setTarget(Number(e.target.value))}><option value="" disabled>Choose a territory</option>{reachable.map(id => <option key={id} value={id}>{map.territories[id].name} · {game.territories[id].troops} troops</option>)}</select></label>{numberInput}</>}
      {game.phase === 'attack' && target !== null && <><div className="tabs combat-tabs"><button type="button" className={blitz ? 'active' : ''} onClick={() => setBlitz(true)}>Blitz</button><button type="button" className={!blitz ? 'active' : ''} onClick={() => setBlitz(false)}>Manual roll</button></div>{!blitz && <label>Attack dice<select value={Math.min(dice, count)} onChange={e => setDice(Number(e.target.value))}>{Array.from({ length: Math.min(3, count) }, (_, i) => <option key={i} value={i + 1}>{i + 1} {i === 0 ? 'die' : 'dice'}</option>)}</select></label>}<button className="primary full" disabled={disabled || !reachable.includes(target)} onClick={() => void session.act({ kind: 'attack', from: selected!, to: target, count, blitz, dice: Math.min(dice, count) })}><Swords size={16} />{blitz ? 'Blitz attack' : 'Roll dice'}</button></>}
      {game.phase === 'fortify' && target !== null && <button className="primary full" disabled={disabled || !reachable.includes(target)} onClick={() => void session.act({ kind: 'fortify', from: selected!, to: target, count })}><Shield size={16} /> Fortify and finish turn</button>}
      <button className="secondary full" disabled={disabled} onClick={() => void session.act({ kind: game.phase === 'attack' ? 'endAttack' : 'endTurn' })}>{game.phase === 'attack' ? 'Finish attacking' : 'Skip fortify · end turn'}<ArrowRight size={16} /></button>
    </>}
  </section>;
}
