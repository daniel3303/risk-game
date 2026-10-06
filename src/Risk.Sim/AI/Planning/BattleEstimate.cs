namespace Risk.Sim.AI.Planning;

public readonly record struct BattleEstimate(double WinChance, double WinningArmies, double LosingDefenders)
{
    public double SurvivorsOnWin => WinChance > 0 ? WinningArmies / WinChance : 0;
    public double DefendersOnLoss => WinChance < 1 ? LosingDefenders / (1 - WinChance) : 0;
}
