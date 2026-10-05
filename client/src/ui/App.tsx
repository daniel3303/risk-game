import { useEffect, useState, useSyncExternalStore } from 'react';
import { CircleHelp, Globe2, X } from 'lucide-react';
import { session } from '../net/session';
import { Home } from './Home';
import { Lobby } from './Lobby';
import { GameScreen } from './GameScreen';
import { Help } from './Help';

export function App() {
  const state = useSyncExternalStore(session.subscribe, session.getSnapshot);
  const [help, setHelp] = useState(false);
  useEffect(() => { void session.restore(); }, []);
  const disabled = state.pending || state.status !== 'connected';
  return <div className={`app ${state.room?.game ? 'in-game' : ''}`}>
    <header className="topbar"><a className="wordmark" href="/" onClick={e => { if (state.room) e.preventDefault(); }}><Globe2 size={27} /><span>RISK <small>GAME</small></span></a><div className="topbar-right"><span className="edition">WORLD DOMINATION</span>{state.room && <span className="connection-status"><span className={`status-dot ${state.status === 'connected' ? 'online' : ''}`} />{state.status === 'connected' ? `Table ${state.room.code}` : state.status}</span>}<button className="help-button" onClick={() => setHelp(true)}><CircleHelp size={17} /><span>How to play</span></button></div></header>
    {state.error && <div className="error-banner" role="alert"><span>{state.error}</span><button className="icon-button" aria-label="Dismiss error" onClick={session.clearError}><X size={16} /></button></div>}
    {state.pending && state.room && <div className="pending-notice" role="status">Sending your move…</div>}
    {(state.room || state.status === 'reconnecting') && state.status !== 'connected' && <div className="reconnect-banner" role="status">{state.status === 'offline' ? 'Connection lost. Your seat is saved in this tab.' : 'Reconnecting to your table…'}{state.status === 'offline' && <><button className="secondary" onClick={() => void session.retry()}>Reconnect</button><button className="text-button" onClick={() => void session.leave()}>Return home</button></>}</div>}
    {!state.room ? <Home pending={state.pending || state.status === 'connecting' || state.status === 'reconnecting'} /> : state.room.game ? <GameScreen room={state.room} seat={state.seat} disabled={disabled} /> : <Lobby room={state.room} seat={state.seat} disabled={disabled} />}
    {!state.room?.game && <footer><span>FORTUNE FAVORS THE THOUGHTFUL.</span><span>Independent project · Classic rules · Original 3D board</span></footer>}
    {help && <Help onClose={() => setHelp(false)} />}
  </div>;
}
