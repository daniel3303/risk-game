namespace Risk.Arena.Models;

public sealed record MatchResult(int Seed, int CandidateSeat, int Winner, bool Finished, int Rounds, int Actions);
