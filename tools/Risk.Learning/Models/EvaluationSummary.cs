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

    /// <summary>The 95% normal interval of the mean over seed blocks, each block's value being its wins over its games.</summary>
    public static (double Lower, double Upper) SeedBlockNormal(int[] blockWins, int players)
    {
        var values = blockWins.Select(w => w / (double)players).ToArray();
        var mean = values.Average();
        var variance = values.Length > 1 ? values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1) : .25;
        var margin = 1.96 * Math.Sqrt(variance / values.Length);
        return (Math.Max(0, mean - margin), Math.Min(1, mean + margin));
    }
}
