import { useEffect, useState } from 'react';
import { Bot, Crown, Flag, Layers, List, LogOut } from 'lucide-react';
import { map, phaseNames, playerColors } from '../game/map';
import { targets } from '../game/moves';
import type { Snapshot } from '../game/types';
import { session } from '../net/session';
import { WorldBoard } from './WorldBoard';
import { Orders } from './Orders';
import { Cards } from './Cards';

export function GameScreen({ room, seat, disabled }: { room: Snapshot; seat: number; disabled: boolean }) {
  const game = room.game!;
  const [selected, setSelected] = useState<number | null>(null);
  const [target, setTarget] = useState<number | null>(null);
  const [showList, setShowList] = useState(false);
  const [showCards, setShowCards] = useState(false);
  const [filter, setFilter] = useState('');
  const reachable = targets(game, selected, seat);
  const current = room.players.find(p => p.id === game.currentPlayer)!;
  const self = room.players.find(p => p.id === seat)!;
  const myTurn = game.currentPlayer === seat;
  useEffect(() => { setTarget(null); }, [game.phase, game.currentPlayer]);
  useEffect(() => { if (game.forcedTrade && myTurn) setShowCards(true); }, [game.forcedTrade, myTurn]);
  const select = (id: number) => {
    if (reachable.includes(id)) setTarget(id);
    else { setSelected(id); setTarget(null); }
  };
  const leave = () => { if (game.phase === 'finished' || window.confirm('Leave this game? An Easy AI will take over your armies.')) void session.leave(); };
  const territory = selected === null ? null : game.territories[selected];
  const owner = room.players.find(p => p.id === territory?.owner);
  return <main className="game-layout">
    <div className="campaign-world"><WorldBoard game={game} selected={selected} reachable={reachable} onSelect={select} />
      <div className="player-strip">{room.players.map(player => <div key={player.id} className={`player-chip ${player.id === game.currentPlayer ? 'current' : ''} ${player.eliminated ? 'eliminated' : ''}`} style={{ '--player-color': playerColors[player.id] } as React.CSSProperties}><span className="player-pin" /><div><strong>{player.name}{player.id === seat && <small> YOU</small>}</strong><span>{player.eliminated ? 'Eliminated' : `${player.territories} lands · ${player.troops} troops · ${player.cards} cards`}</span></div>{player.isBot && <Bot size={14} />}{!player.isBot && !player.connected && <small>OFFLINE</small>}</div>)}</div>
      <div className="world-toolbar"><button className={`secondary ${showList ? 'pressed' : ''}`} aria-expanded={showList} onClick={() => setShowList(v => !v)}><List size={16} /> Territory list</button><button className="secondary" aria-expanded={showCards} onClick={() => setShowCards(v => !v)}><Layers size={16} /> Your cards <span className="badge">{game.hand.length}</span></button></div>
      {showList && <section className="territory-list" aria-label="Territory list"><label className="sr-only" htmlFor="search-territories">Search territories</label><input id="search-territories" placeholder="Find a territory…" value={filter} onChange={e => setFilter(e.target.value)} /><div>{map.territories.filter(t => t.name.toLowerCase().includes(filter.toLowerCase())).map(definition => {
        const t = game.territories[definition.id];
        return <button key={t.id} data-testid={`territory-${t.id}`} data-owner={t.owner} className={selected === t.id ? 'selected' : reachable.includes(t.id) ? 'reachable' : ''} onClick={() => select(t.id)}><span className="player-pin" style={{ background: t.owner >= 0 ? playerColors[t.owner] : '#718579' }} /><span>{definition.name}</span><strong>{t.troops}</strong></button>;
      })}</div></section>}
      {showCards && <Cards room={room} game={game} seat={seat} disabled={disabled} onClose={() => setShowCards(false)} />}
    </div>
    <aside className="campaign-panel">
      {game.phase === 'finished' ? <div className="victory"><Crown size={38} /><span className="eyebrow">WORLD DOMINATION</span><h2>{room.players.find(p => p.id === game.winner)?.name} wins.</h2><p className="muted">Every campaign writes its own history.</p><button className="primary full" onClick={leave}>Start a new campaign</button></div> : <>
        <div className="turn-label"><span className="status-dot online" /><span data-testid="turn-status">{myTurn ? 'YOUR TURN' : current.isBot ? 'AI IS THINKING' : `${current.name.toUpperCase()}'S TURN`}</span><span>ROUND {game.round}</span></div>
        <h1 className="phase-title" data-testid="phase">{phaseNames[game.phase]}</h1>
        <div className="phase-steps">{['draft', 'attack', 'fortify'].map((phase, i) => <div key={phase} className={game.phase === phase || (phase === 'attack' && game.phase === 'occupy') ? 'active' : ''}><span>{i + 1}</span>{phase}</div>)}</div>
        {(game.phase === 'draft' || game.phase === 'setup') && <div className="reinforcements"><Flag size={20} /><strong>{game.phase === 'setup' ? game.setupTroops : game.reinforcements}</strong><span>{game.phase === 'setup' ? 'starting troops left' : 'troops to deploy'}</span></div>}
        {territory && <div className="selected-territory"><span className="small-label">SELECTED TERRITORY</span><h2>{map.territories[selected!].name}</h2><div><span className="player-pin" style={{ background: owner ? playerColors[owner.id] : '#7b867b' }} />{owner?.name ?? 'Unclaimed'}<strong>{territory.troops} troops</strong></div></div>}
        <Orders game={game} seat={seat} selected={selected} target={target} reachable={reachable} setTarget={setTarget} disabled={disabled} />
      </>}
      <div className="continent-bonuses"><div className="section-label">CONTINENT BONUSES</div>{map.continents.map(c => <div key={c.id}><span style={{ background: c.color }} /><span>{c.name}</span><strong>+{c.bonus}</strong></div>)}</div>
      {game.battle && <div className="battle-result"><span className="small-label">LAST BATTLE · {map.territories[game.battle.to].name}</span><div><span>Attack {game.battle.attackDice.join(' · ')}</span><span>Defend {game.battle.defendDice.join(' · ')}</span></div><p>Attacker −{game.battle.attackerLosses} · Defender −{game.battle.defenderLosses}{game.battle.captured ? ' · Captured' : ''}</p></div>}
      <details className="activity"><summary>Campaign journal</summary><ol>{game.log.slice(-8).reverse().map((entry, i) => <li key={`${game.log.length}-${i}`}>{entry}</li>)}</ol></details>
      <div className="game-exit">{!self.eliminated && game.phase !== 'finished' && <button className="text-button" disabled={disabled || game.phase === 'occupy' || game.phase === 'claim' || game.phase === 'setup'} onClick={() => { if (window.confirm('Surrender your campaign? Your armies remain on the board and you can watch the remaining game.')) void session.act({ kind: 'surrender' }); }}>Surrender</button>}<button className="text-button" disabled={disabled} onClick={leave}><LogOut size={14} /> Leave table</button></div>
    </aside>
  </main>;
}
