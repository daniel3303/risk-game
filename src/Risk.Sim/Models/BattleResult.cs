namespace Risk.Sim.Models;

public sealed record BattleResult(int From, int To, int AttackerLosses, int DefenderLosses, int[] AttackDice, int[] DefendDice, bool Captured);
