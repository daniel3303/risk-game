import { MapPin, Swords } from 'lucide-react';
import { playerColors } from '../game/map';
import type { Player } from '../game/types';
import { CommanderPortrait } from './CommanderPortrait';

export function PlayerStrip({ players, currentPlayer, seat }: { players: Player[]; currentPlayer: number; seat: number }) {
  return <div className="player-strip" aria-label="Commanders">{players.map(player => <div key={player.id} className={`player-chip ${player.id === currentPlayer ? 'current' : ''} ${player.eliminated ? 'eliminated' : ''}`} style={{ '--player-color': playerColors[player.id] } as React.CSSProperties} title={`${player.name}${player.id === seat ? ' · You' : ''} · ${player.troops} troops · ${player.territories} territories · ${player.cards} cards${!player.isBot && !player.connected ? ' · Offline' : ''}`}>
    <div className="portrait-frame"><CommanderPortrait player={player.id} bot={player.isBot} /><span className="rank-token" aria-label={`${player.cards} cards`}><span>{player.cards}</span></span></div>
    <div className="player-stats"><span><Swords size={25} /><strong>{player.troops}</strong></span><span><MapPin size={25} /><strong>{player.territories}</strong></span><small>{player.name}{player.id === seat ? ' · YOU' : ''}</small></div>
  </div>)}</div>;
}
