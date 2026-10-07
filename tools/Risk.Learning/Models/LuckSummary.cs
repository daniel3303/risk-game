namespace Risk.Learning.Models;

public sealed record LuckSummary(int Deals, int Replays, double FirstSeatWinRate, double DealsFirstSeatAtLeast90, double DealsFirstSeatAtMost10,
    double DealsBetween30And70, double OutcomeVariance, double DealVariance, double DiceVariance);
