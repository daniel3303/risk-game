namespace Risk.Arena.Models;

public sealed record ArenaReport(ArenaOptions Options, int Games, int Wins, int Losses, int Unfinished,
    double WinRate, double ConfidenceLower, double ConfidenceUpper, string ConfidenceMethod, MatchResult[] Matches)
{
    public static ArenaReport Build(ArenaOptions options, MatchResult[] matches)
    {
        var wins = matches.Count(m => m.Finished && m.Winner == m.CandidateSeat);
        var losses = matches.Count(m => m.Finished && m.Winner != m.CandidateSeat);
        var rate = wins / (double)matches.Length;
        // Matches sharing a seed form one bounded observation, not independent trials.
        var margin = Math.Sqrt(Math.Log(40) / (2 * options.Seeds));
        return new(options, matches.Length, wins, losses, matches.Count(m => !m.Finished), rate,
            Math.Max(0, rate - margin), Math.Min(1, rate + margin), "95% Hoeffding bound over seed blocks; unfinished games count as non-wins", matches);
    }
}
