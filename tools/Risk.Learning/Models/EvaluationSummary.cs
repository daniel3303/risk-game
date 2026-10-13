using Newtonsoft.Json;
namespace Risk.Learning.Models;

/// <summary>
/// Seat-balanced result of a model evaluation; in defend mode the rate is the opponent's, who rotates through the seats. The
/// confidence bounds are the conservative Hoeffding bound over seed blocks; the normal bounds are the 95% normal interval over
/// the same blocks, which the promotion gate uses.
/// </summary>
public sealed record EvaluationSummary(string Opponent, int Players, bool Defend, int FirstSeed, int Seeds, int[] SeatWins, int Unfinished,
    double WinRate, double ConfidenceLower, double ConfidenceUpper, double NormalLower, double NormalUpper)
{
    public double FairShare => 1.0 / Players;

    /// <summary>Each unfinished game as seed/seat with every player's final territories and armies, in seat order.</summary>
    public IReadOnlyList<string> UnfinishedGames { get; init; } = [];

    /// <summary>The wins of each seed block, one block per seed with every seat; kept for paired comparisons, not reported.</summary>
    [JsonIgnore] public int[] BlockWins { get; init; } = [];

    /// <summary>The 95% normal interval of the mean over seed blocks, each block's value being its wins over its games.</summary>
    public static (double Lower, double Upper) SeedBlockNormal(int[] blockWins, int players) => Normal(blockWins.Select(w => w / (double)players).ToArray());

    /// <summary>
    /// The 95% normal interval of the mean per-seed difference, first minus second, between two evaluations of the same seeds;
    /// pairing cancels the starting deal each seed gives both evaluations, which comparing their separate intervals cannot.
    /// </summary>
    public static (double Lower, double Upper) PairedNormal(int[] first, int[] second, int players)
    {
        if (first.Length != second.Length || first.Length == 0) throw new ArgumentException("Paired evaluations must cover the same seeds.");
        return Normal(first.Zip(second, (a, b) => (a - b) / (double)players).ToArray(), -1);
    }

    private static (double Lower, double Upper) Normal(double[] values, double floor = 0)
    {
        var mean = values.Average();
        var variance = values.Length > 1 ? values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1) : .25;
        var margin = 1.96 * Math.Sqrt(variance / values.Length);
        return (Math.Max(floor, mean - margin), Math.Min(1, mean + margin));
    }
}
