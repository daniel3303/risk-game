using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>
/// The learned correction played by the Ultimate difficulty: one model for duels and, when bundled, another for boards with
/// more than two players. Each model declines boards outside its encoding, so the two never apply at once.
/// </summary>
public sealed class UltimateValuation(ValueModel duel, ValueModel multiplayer) : IBoardValuation
{
    private static readonly Lazy<UltimateValuation> bundled = new(() => new(ValueModel.Ultimate, ValueModel.UltimateMultiplayer));

    /// <summary>The models embedded in this build.</summary>
    public static UltimateValuation Bundled => bundled.Value;

    public ValueModel Duel { get; } = duel;
    public ValueModel Multiplayer { get; } = multiplayer;

    public double Correction(PositionEvaluator evaluator, PlannerBoard board, double score) =>
        (Duel?.Correction(evaluator, board, score) ?? 0) + (Multiplayer?.Correction(evaluator, board, score) ?? 0);
}
