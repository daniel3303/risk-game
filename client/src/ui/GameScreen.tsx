import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { ChartColumnIncreasing, CircleHelp, Crown, Dices, Layers, List, LogOut, Settings, X } from 'lucide-react';
import { map, playerColors } from '../game/map';
import { targets } from '../game/moves';
import type { Snapshot } from '../game/types';
import { session } from '../net/session';
import { WorldBoard } from './WorldBoard';
import { Orders } from './Orders';
import { Cards } from './Cards';
import { BattleResult } from './BattleResult';
import { PlayerStrip } from './PlayerStrip';
import { TurnMedallion } from './TurnMedallion';

export function GameScreen({ room, seat, disabled, onHelp }: { room: Snapshot; seat: number; disabled: boolean; onHelp: () => void }) {
  const game = room.game!;
  const layout = useRef<HTMLElement>(null);
  const panel = useRef<HTMLElement>(null);
  const menu = useRef<HTMLDetailsElement>(null);
  const journal = useRef<HTMLDetailsElement>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [target, setTarget] = useState<number | null>(null);
  const [showList, setShowList] = useState(false);
  const [showCards, setShowCards] = useState(false);
  const [showContinents, setShowContinents] = useState(false);
  const [filter, setFilter] = useState('');
  const reachable = targets(game, selected, seat);
  const current = room.players.find(p => p.id === game.currentPlayer)!;
  const self = room.players.find(p => p.id === seat);
  const spectating = room.spectators.some(s => s.id === seat);
  const myTurn = !spectating && game.currentPlayer === seat;
  const hasOrders = myTurn && (selected !== null || ['claim', 'occupy', 'attack', 'fortify'].includes(game.phase));
  useLayoutEffect(() => {
    const resize = () => layout.current?.style.setProperty('--turn-panel-height', `${Math.ceil(panel.current!.getBoundingClientRect().height)}px`);
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(panel.current!);
    return () => observer.disconnect();
  }, []);
  useEffect(() => { setTarget(null); }, [game.phase, game.currentPlayer]);
  useEffect(() => { if (game.forcedTrade && myTurn) setShowCards(true); }, [game.forcedTrade, myTurn]);
  const select = (id: number) => {
    setShowList(false);
    if (reachable.includes(id)) setTarget(id);
    else { setSelected(id); setTarget(null); }
  };
  const leave = () => { if (spectating || game.phase === 'finished' || window.confirm('Leave this game? An Easy AI will take over your armies.')) void session.leave(); };
  const territory = selected === null ? null : game.territories[selected];
  const owner = room.players.find(p => p.id === territory?.owner);
  return <main className="game-layout" ref={layout}>
    <div className="campaign-world"><WorldBoard game={game} selected={selected} reachable={reachable} onSelect={select} continentOverlay={showContinents} viewer={spectating ? -1 : seat} />
      <PlayerStrip players={room.players} currentPlayer={game.currentPlayer} seat={seat} />
      <div className="game-quick-buttons"><button className="hud-round" aria-label="How to play" title="How to play" onClick={onHelp}><CircleHelp size={38} /></button><button className="hud-round" aria-label="Battle history" title="Battle history" onClick={() => { menu.current!.open = true; journal.current!.open = true; }}><Dices size={36} /></button></div>
      <div className="world-toolbar"><button className={`secondary territory-list-toggle ${showList ? 'pressed' : ''}`} aria-label="Territory list" title="Territory list" aria-expanded={showList} onClick={() => setShowList(v => !v)}><List size={28} /></button><button className={`secondary continent-toggle ${showContinents ? 'pressed' : ''}`} aria-label="Continent bonuses" title="Continent bonuses" aria-pressed={showContinents} onClick={() => setShowContinents(v => !v)}><ChartColumnIncreasing size={40} /></button>{!spectating && <button className="secondary card-deck-button" aria-label="Your cards" title="Your cards" aria-expanded={showCards} onClick={() => setShowCards(v => !v)}><Layers size={23} /><span className="badge">{game.hand.length}</span></button>}</div>
      <details className="game-menu" ref={menu}><summary aria-label="Game menu" title="Game menu"><Settings size={36} /></summary><div className="game-exit">{self && !self.eliminated && game.phase !== 'finished' && <button className="text-button" disabled={disabled || game.phase === 'occupy' || game.phase === 'claim' || game.phase === 'setup'} onClick={() => { if (window.confirm('Surrender your campaign? Your armies remain on the board and you can watch the remaining game.')) void session.act({ kind: 'surrender' }); }}>Surrender</button>}<button className="text-button" disabled={disabled} onClick={leave}><LogOut size={14} /> Leave table</button><details className="activity" ref={journal}><summary>Campaign journal</summary>{game.battle && <div className="history-battle"><BattleResult battle={game.battle} /></div>}<ol>{game.log.slice(-8).reverse().map((entry, i) => <li key={`${game.log.length}-${i}`}>{entry}</li>)}</ol></details></div></details>
      {showList && <section className="territory-list" aria-label="Territory list"><div className="territory-search"><label className="sr-only" htmlFor="search-territories">Search territories</label><input id="search-territories" placeholder="Find a territory…" value={filter} onChange={e => setFilter(e.target.value)} /><button className="icon-button" aria-label="Close territory list" onClick={() => setShowList(false)}><X size={17} /></button></div><div className="territory-choices">{map.territories.filter(t => t.name.toLowerCase().includes(filter.toLowerCase())).map(definition => {
        const t = game.territories[definition.id];
        return <button key={t.id} data-testid={`territory-${t.id}`} data-owner={t.owner} className={selected === t.id ? 'selected' : reachable.includes(t.id) ? 'reachable' : ''} onClick={() => select(t.id)}><span className="player-pin" style={{ background: t.owner >= 0 ? playerColors[t.owner] : '#718579' }} /><span>{definition.name}</span><strong>{t.troops}</strong></button>;
      })}</div></section>}
      {showCards && !spectating && <Cards room={room} game={game} seat={seat} disabled={disabled} onClose={() => setShowCards(false)} />}
    </div>
    <aside className={`campaign-panel ${hasOrders ? 'has-orders' : ''}`} ref={panel}>
      {game.phase === 'finished' ? <div className="victory"><Crown size={38} /><span className="eyebrow">WORLD DOMINATION</span><h2>{room.players.find(p => p.id === game.winner)?.name} wins.</h2><p className="muted">Every campaign writes its own history.</p><button className="primary full" onClick={leave}>Start a new campaign</button></div> : <>
        {hasOrders && <div className="orders-panel">
        {territory && <div className="selected-territory"><span className="small-label">SELECTED TERRITORY</span><h2>{map.territories[selected!].name}</h2><div><span className="player-pin" style={{ background: owner ? playerColors[owner.id] : '#7b867b' }} />{owner?.name ?? 'Unclaimed'}<strong>{territory.troops} troops</strong></div></div>}
        <Orders game={game} seat={seat} selected={selected} target={target} reachable={reachable} setTarget={setTarget} disabled={disabled} />
        </div>}
        <TurnMedallion game={game} current={current} myTurn={myTurn} spectating={spectating} />
      </>}
      {game.battle && <><div className="battle-result"><BattleResult battle={game.battle} /></div><p className="mobile-battle-summary" role="status">Last battle · {map.territories[game.battle.to].name}: Attacker −{game.battle.attackerLosses} · Defender −{game.battle.defenderLosses}{game.battle.captured ? ' · Captured' : ''}</p></>}
    </aside>
  </main>;
}
