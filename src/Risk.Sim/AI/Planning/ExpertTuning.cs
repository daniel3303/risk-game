namespace Risk.Sim.AI.Planning;

/// <summary>Weights and search limits of the Expert planner; the defaults are the published Expert policy.</summary>
public sealed record ExpertTuning
{
    public static ExpertTuning Default { get; } = new();

    /// <summary>Selected on development seeds against Expert; see docs/master-ai.md.</summary>
    public static ExpertTuning Master { get; } = new()
    {
        DuelEnemyIncomeValue = 8, BeamWidth = 16, Branches = 20, MaxDepth = 10, SearchBudget = 4096, DeployBorders = 24,
    };

    public double TerritoryValue { get; init; } = 1.4;
    public double IncomeValue { get; init; } = 4;
    public double DuelEnemyArmyValue { get; init; } = .9;
    public double DuelEnemyIncomeValue { get; init; } = 4;
    public double MultiplayerEnemyArmyValue { get; init; } = .3;
    public double MultiplayerEnemyIncomeValue { get; init; } = 1.2;
    public double EliminationValueBase { get; init; } = 8;
    public double InheritedCardMultiplier { get; init; } = 1.5;
    public double ContinentExposureValue { get; init; } = 3;
    public double FixedSetValue { get; init; } = 8;
    public double AttackThreshold { get; init; } = .6;
    public double ContinuationThreshold { get; init; } = .78;
    public double DuelContinuationThreshold { get; init; } = .6;
    public int BeamWidth { get; init; } = 8;
    public int Branches { get; init; } = 10;
    public int MaxDepth { get; init; } = 6;
    public int SearchBudget { get; init; } = 512;
    public int DeployBorders { get; init; } = 8;
    public int FortifySources { get; init; } = 6;
    public int FortifyTargets { get; init; } = 4;
    /// <summary>Optional learned correction added to the hand-written score of every position that is not already won.</summary>
    public IBoardValuation Valuation { get; init; }
}
