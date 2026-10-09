using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>A fixed-width feature encoding of a planned board from the mover's perspective; each value model names the encoding it was trained on.</summary>
public abstract class BoardEncoding
{
    public const double ScoreScale = 50;
    public static BoardEncoding Duel { get; } = new DuelBoardFeatures();
    public static BoardEncoding Multiplayer { get; } = new MultiplayerBoardFeatures();

    public abstract string Version { get; }
    public abstract int Count { get; }
    /// <summary>Index of the hand-written score, scaled by <see cref="ScoreScale"/>, which the model's logistic term calibrates.</summary>
    public abstract int ScoreIndex { get; }
    public abstract bool Supports(PositionEvaluator evaluator);
    /// <summary>Writes the features of <paramref name="board"/>; <paramref name="score"/> is the hand-written evaluation of the same board.</summary>
    public abstract void Extract(PositionEvaluator evaluator, PlannerBoard board, double score, float[] x);

    public static BoardEncoding For(string version) => version switch
    {
        _ when version == Duel.Version => Duel,
        _ when version == Multiplayer.Version => Multiplayer,
        _ => null,
    };
}
