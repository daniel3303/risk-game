namespace Risk.Learning.Models;

public sealed record EvaluationSummary(string Opponent, int FirstSeed, int Seeds, int FirstSeatWins, int SecondSeatWins, int Unfinished,
    double WinRate, double ConfidenceLower, double ConfidenceUpper);
