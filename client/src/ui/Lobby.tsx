import { useState } from 'react';
import { ArrowRight, Bot, Check, Copy, Plus, Users, X } from 'lucide-react';
import { session } from '../net/session';
import { playerColors } from '../game/map';
import type { Difficulty, Snapshot } from '../game/types';
import { WorldBoard } from './WorldBoard';
import { CommanderPortrait } from './CommanderPortrait';

const descriptions = { easy: 'A cautious recruit. Protects armies and looks for simple wins.', normal: 'A tactician. Builds a strong frontier and pursues continents.', hard: 'A strategist. Samples battle outcomes before committing troops.', expert: 'Plans connected conquests, hunts eliminations, and protects continent income.', master: 'Searches deeper capture chains and denies rival income. Beats Expert in duels.' };
const implementations = { easy: 'Heuristic', normal: 'Heuristic', hard: 'Monte Carlo', expert: 'Turn planner', master: 'Deep planner' };

export function Lobby({ room, seat, disabled }: { room: Snapshot; seat: number; disabled: boolean }) {
  const host = room.host === seat;
  const [difficulty, setDifficulty] = useState<Difficulty>('normal');
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState('');
  const invite = `${location.origin}${location.pathname}?room=${room.code}`;
  const copy = async () => {
    try { await navigator.clipboard.writeText(invite); setCopied(true); setCopyError(''); }
    catch { setCopyError('Copy the invite link below to share this table.'); }
  };
  return <main className="lobby-layout">
    <section className="lobby-panel">
      <div className="eyebrow">YOUR CAMPAIGN BEGINS HERE</div><h1>The war room</h1><p className="muted">{room.aiOnly ? 'You are a spectator. Add 2–6 AI commanders and watch their strategies unfold.' : 'Bring your friends. Fill the remaining seats with AI.'}</p>
      {room.aiOnly && <p className="spectator-lobby" data-testid="spectator-status">{room.spectators.map(s => `${s.name}${s.id === seat ? ' (you)' : ''}${s.id === room.host ? ' · Host' : ''}${s.connected ? '' : ' · Offline'}`).join(' / ')} · Spectators</p>}
      <div className="invite-card"><div><span className="small-label">ROOM CODE</span><strong data-testid="room-code">{room.code}</strong></div><button className="secondary" onClick={() => void copy()}>{copied ? <Check size={16} /> : <Copy size={16} />}{copied ? 'Copied' : 'Copy invite'}</button></div>
      <input className="invite-url" aria-label="Invite link" value={invite} readOnly onFocus={e => e.target.select()} />
      {copyError && <p role="status">{copyError}</p>}
      <div className="section-label"><Users size={15} /> COMMANDERS <span>{room.players.length} / 6</span></div>
      <div className="seats">{Array.from({ length: 6 }, (_, id) => {
        const player = room.players.find(p => p.id === id);
        return player ? <div className="seat" key={id}><div className="seat-avatar" style={{ '--player-color': playerColors[id] } as React.CSSProperties}><CommanderPortrait player={id} bot={player.isBot} /></div><div className="seat-name"><strong>{player.name} {player.id === seat && <small>YOU</small>}</strong><span>{player.isBot ? `${player.difficulty} · ${implementations[player.difficulty]}` : player.id === room.host ? 'Table host' : player.connected ? 'Ready to play' : 'Reconnecting…'}</span></div>{player.isBot && host ? <button className="icon-button" aria-label={`Remove ${player.name}`} disabled={disabled} onClick={() => void session.invoke('RemoveBot', id)}><X size={16} /></button> : <span className={`status-dot ${player.connected || player.isBot ? 'online' : ''}`} />}</div> : <div className="seat empty-seat" key={id}><div className="seat-avatar"><Plus size={20} /></div><span>{room.aiOnly ? 'Seat open for an AI commander' : 'Seat open for a friend or AI'}</span></div>;
      })}</div>
      {host && <div className="ai-controls"><div className="form-row"><label>AI difficulty<select value={difficulty} onChange={e => setDifficulty(e.target.value as Difficulty)}><option value="easy">Easy · Recruit</option><option value="normal">Normal · Tactician</option><option value="hard">Hard · Strategist</option><option value="expert">Expert · Commander</option><option value="master">Master · Warlord</option></select></label><button className="secondary" disabled={disabled || room.players.length === 6} onClick={() => void session.invoke('AddBot', difficulty)}><Bot size={16} /> Add AI player</button></div><p className="muted tiny">{descriptions[difficulty]}</p></div>}
      <div className="lobby-rules"><span>CLASSIC WORLD</span><span>{room.options.cards.toUpperCase()} CARDS</span><span>{room.options.setup.toUpperCase()} SETUP</span></div>
      {host ? <button className="primary full" disabled={disabled || room.players.length < 2 || room.players.some(p => !p.isBot && !p.connected)} onClick={() => void session.invoke('Start')}>Begin World Domination<ArrowRight size={18} /></button> : <div className="waiting-message">Waiting for the host to begin the campaign…</div>}
      <button className="text-button full" disabled={disabled} onClick={() => void session.leave()}>Leave table</button>
    </section>
    <div className="lobby-map"><WorldBoard game={null} onSelect={() => {}} preview /><div className="map-plaque"><span>A WORLD WORTH FIGHTING FOR</span><strong>Choose your allies.<br />Then your battles.</strong></div></div>
  </main>;
}
