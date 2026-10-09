namespace Risk.Learning.Models;

/// <summary>Seat-balanced result of a model evaluation; in defend mode the rate is the opponent's, who rotates through the seats.</summary>
public sealed record EvaluationSummary(string Opponent, int Players, bool Defend, int FirstSeed, int Seeds, int[] SeatWins, int Unfinished,
    double WinRate, double ConfidenceLower, double ConfidenceUpper)
{
    public double FairShare => 1.0 / Players;
}
