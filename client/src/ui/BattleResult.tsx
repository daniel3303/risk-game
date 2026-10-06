import { map } from '../game/map';
import type { Battle } from '../game/types';
import { Dice } from './Dice';

export function BattleResult({ battle }: { battle: Battle }) {
  return <>
    <span className="small-label">LAST BATTLE · {map.territories[battle.to].name}</span>
    <div className="dice-tray"><span>{battle.attackDice.map((value, i) => <Dice key={i} value={value} attack />)}</span><span className="battle-versus">VS</span><span>{battle.defendDice.map((value, i) => <Dice key={i} value={value} />)}</span></div>
    <p>Attacker −{battle.attackerLosses} · Defender −{battle.defenderLosses}{battle.captured ? ' · Captured' : ''}</p>
  </>;
}
