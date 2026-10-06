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
        var matches = new List<MatchResult>();
        for (var seed = options.FirstSeed; seed < options.FirstSeed + options.Seeds; seed++)
            for (var seat = 0; seat < options.Players; seat++)
            {
                cancellation.ThrowIfCancellationRequested();
                matches.Add(Play(map, options, seed, seat, cancellation));
            }
        return ArenaReport.Build(options, matches.ToArray());
    }

    private static MatchResult Play(WorldMap map, ArenaOptions options, int seed, int candidateSeat, CancellationToken cancellation)
    {
        var difficulties = Enumerable.Range(0, options.Players).Select(seat => seat == candidateSeat ? options.Candidate : options.Opponent).ToArray();
        var names = difficulties.Select((difficulty, seat) => $"{difficulty} {seat + 1}").ToArray();
        var game = new Game(map, names, new(options.Cards, options.Setup), new SeededRandom(seed));
        var strategies = difficulties.Select((difficulty, seat) => StrategyCatalog.Create(difficulty, unchecked(seed * 1000003 + seat * 7919))).ToArray();
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
