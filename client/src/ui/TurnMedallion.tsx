import { phaseNames, playerColors } from '../game/map';
import type { GameView, Player } from '../game/types';
import { CommanderPortrait } from './CommanderPortrait';

export function TurnMedallion({ game, current, myTurn, spectating = false }: { game: GameView; current: Player; myTurn: boolean; spectating?: boolean }) {
  const reserve = game.phase === 'draft' ? game.reinforcements : game.phase === 'setup' ? game.setupTroops : current.troops;
  return <div className="turn-medallion" style={{ '--player-color': playerColors[current.id] } as React.CSSProperties}>
    <div className="medallion-portrait portrait-frame"><CommanderPortrait player={current.id} bot={current.isBot} /><span className="rank-token"><span>{current.cards}</span></span></div>
    <div className="commander-bar"><strong>{current.name}</strong><span data-testid="turn-status">{spectating ? `SPECTATING · ROUND ${game.round}` : myTurn ? 'YOUR TURN' : current.isBot ? 'AI IS THINKING' : `${current.name.toUpperCase()}'S TURN`}</span><h1 className="phase-title" data-testid="phase">{phaseNames[game.phase]}</h1></div>
    <div className="army-reserve" aria-label={`${reserve} ${game.phase === 'draft' || game.phase === 'setup' ? 'troops to deploy' : 'troops'}`}><div className="army-statue"><img src="/assets/infantry.png" alt="" draggable={false} /></div><strong>{reserve}</strong></div>
  </div>;
}
