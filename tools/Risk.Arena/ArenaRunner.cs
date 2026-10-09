using Risk.Arena.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Arena;

public static class ArenaRunner
{
    public static ArenaReport Run(WorldMap map, ArenaOptions options, CancellationToken cancellation = default)
    {
        options.Validate();
        var jobs = Enumerable.Range(options.FirstSeed, options.Seeds).SelectMany(seed => Enumerable.Range(0, options.Players).Select(seat => (Seed: seed, Seat: seat))).ToArray();
        var matches = new MatchResult[jobs.Length];
        // Each match owns its dice and strategy random sources, so parallel order cannot change any result.
        Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = options.Parallelism, CancellationToken = cancellation },
            i => matches[i] = Play(map, options, jobs[i].Seed, jobs[i].Seat, cancellation));
        return ArenaReport.Build(options, matches);
    }

    private static MatchResult Play(WorldMap map, ArenaOptions options, int seed, int candidateSeat, CancellationToken cancellation)
    {
        var policies = Enumerable.Range(0, options.Players).Select(seat => seat == candidateSeat ? options.Candidate : options.Opponent).ToArray();
        var names = policies.Select((policy, seat) => $"{char.ToUpperInvariant(policy[0])}{policy[1..]} {seat + 1}").ToArray();
        var game = new Game(map, names, new(options.Cards, options.Setup), new SeededRandom(seed));
        var strategies = policies.Select((policy, seat) => PolicyCatalog.Create(policy, unchecked(seed * 1000003 + seat * 7919))).ToArray();
        var actions = 0;
        while (game.State.Phase != Phase.Finished && game.State.Round <= options.MaxRounds && actions < options.MaxActions)
        {
            cancellation.ThrowIfCancellationRequested();
            var player = game.State.CurrentPlayer;
            game.Apply(player, strategies[player].Choose(GameObservation.From(game)));
            actions++;
        }
        return new(seed, candidateSeat, game.State.Winner, game.State.Phase == Phase.Finished, game.State.Round, actions);
    }
}
