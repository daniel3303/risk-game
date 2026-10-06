import { useState } from 'react';
import { ArrowRight, Globe2, Shield, Swords, Users } from 'lucide-react';
import { session } from '../net/session';
import type { CardMode, SetupMode } from '../game/types';
import { WorldBoard } from './WorldBoard';

export function Home({ pending }: { pending: boolean }) {
  const invite = new URLSearchParams(location.search).get('room') ?? '';
  const [mode, setMode] = useState<'create' | 'join'>(invite ? 'join' : 'create');
  const [name, setName] = useState(localStorage.getItem('risk-name') ?? '');
  const [code, setCode] = useState(invite);
  const [cards, setCards] = useState<CardMode>('fixed');
  const [setup, setSetup] = useState<SetupMode>('automatic');
  const [spectate, setSpectate] = useState(false);
  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    localStorage.setItem('risk-name', name.trim());
    if (mode === 'create') void session.create(name.trim(), { cards, setup }, spectate);
    else void session.join(code.trim(), name.trim());
  };
  return <main className="home">
    <div className="home-copy">
      <div className="eyebrow"><span className="gold-line" /> THE WORLD IS YOURS TO TAKE</div>
      <h1>Conquer<br />the <em>world.</em></h1>
      <p className="home-description">Rally your friends. Challenge your rivals.<br />Your next great campaign starts here.</p>
      <form className="entry-card" onSubmit={submit}>
        <div className="tabs"><button type="button" className={mode === 'create' ? 'active' : ''} onClick={() => setMode('create')}>Create a table</button><button type="button" className={mode === 'join' ? 'active' : ''} onClick={() => setMode('join')}>Join friends</button></div>
        <label>Your commander name<input name="name" autoComplete="nickname" required minLength={1} maxLength={24} value={name} onChange={e => setName(e.target.value)} placeholder="What should we call you?" /></label>
        {mode === 'create' && <label>Your role<select value={spectate ? 'spectator' : 'player'} onChange={e => setSpectate(e.target.value === 'spectator')}><option value="player">Play as a commander</option><option value="spectator">Spectate an AI-only game</option></select></label>}
        {mode === 'create' ? <div className="form-row"><label>Card bonuses<select value={cards} onChange={e => setCards(e.target.value as CardMode)}><option value="fixed">Fixed · continent strategy</option><option value="progressive">Progressive · growing armies</option></select></label><label>Starting territories<select value={setup} onChange={e => setSetup(e.target.value as SetupMode)}><option value="automatic">Automatic</option><option value="manual">Choose in turns</option></select></label></div> : <label>Six-letter room code<input name="code" className="code-input" required pattern="[A-Za-z]{6}" maxLength={6} value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder="ABCDEF" /></label>}
        <button className="primary full" disabled={pending}>{pending ? 'Connecting…' : mode === 'create' ? spectate ? 'Create AI room' : 'Create your table' : 'Join the table'}<ArrowRight size={18} /></button>
        <div className="entry-note"><Shield size={13} /> No account needed. Friends and AI welcome.</div>
      </form>
      <div className="home-features"><span><Users size={16} /> 2–6 commanders</span><span><Globe2 size={16} /> The Classic world</span><span><Swords size={16} /> True Random dice</span></div>
    </div>
    <div className="home-map"><WorldBoard game={null} onSelect={() => {}} preview /><div className="map-plaque"><span>THE CLASSIC CAMPAIGN</span><strong>World Domination</strong><p>42 territories. Six continents. Your next move.</p></div></div>
  </main>;
}
