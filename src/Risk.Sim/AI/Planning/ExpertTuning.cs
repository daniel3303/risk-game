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

    /// <summary>Master's search plus frontier defence and threat weighting in games with more than two players; see docs/ultimate-ai.md.</summary>
    public static ExpertTuning Ultimate { get; } = Master with { FrontierRiskValue = 1, ThreatWeighting = 1.5 };

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
    /// <summary>
    /// Weight, in games with more than two players, of the expected loss from weakly held borders: each own border territory's
    /// capture chance by the strongest adjacent enemy stack times what falls with it, plus the card the capturer draws. Zero disables the term.
    /// </summary>
    public double FrontierRiskValue { get; init; }
    /// <summary>The same weight while exactly two players remain. Whenever frontier risk is scored, the planner also considers occupations that leave the source a garrison able to resist the strongest adjacent enemy stack.</summary>
    public double DuelFrontierRiskValue { get; init; }
    /// <summary>
    /// In games with more than two players, rival armies and income are weighted by that rival's strength relative to the
    /// average rival, raised to this exponent; zero weights every rival alike.
    /// </summary>
    public double ThreatWeighting { get; init; }
}
