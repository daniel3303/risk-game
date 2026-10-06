namespace Risk.Sim.AI.Planning;

public readonly record struct DiceOutcome(int AttackerLosses, int DefenderLosses, double Probability);
