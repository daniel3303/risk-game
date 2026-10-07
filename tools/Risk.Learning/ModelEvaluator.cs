using Risk.Learning.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>Plays Master's planner with a candidate value model against a catalogue opponent, every seed from both seats.</summary>
public static class ModelEvaluator
{
    public static EvaluationSummary Run(WorldMap map, ValueModel model, BotDifficulty opponent, int firstSeed, int seeds, int threads)
    {
        int first = 0, second = 0, unfinished = 0;
        Parallel.For(0, seeds * 2, new ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
        {
            var seed = firstSeed + job / 2;
            var seat = job % 2;
            var game = new Game(map, ["A", "B"], new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
            var strategies = new IPlayerStrategy[2];
            strategies[seat] = new ExpertStrategy(ExpertTuning.Master with { Valuation = model });
            strategies[1 - seat] = StrategyCatalog.Create(opponent, unchecked(seed * 1000003 + (1 - seat) * 7919));
            var actions = 0;
            while (game.State.Phase != Phase.Finished && game.State.Round <= 200 && actions++ < 20000)
                game.Apply(game.State.CurrentPlayer, strategies[game.State.CurrentPlayer].Choose(GameObservation.From(game)));
            if (game.State.Phase != Phase.Finished) Interlocked.Increment(ref unfinished);
            else if (game.State.Winner == seat) Interlocked.Increment(ref seat == 0 ? ref first : ref second);
        });
        var rate = (first + second) / (2.0 * seeds);
        // Same conservative Hoeffding bound over seed blocks as the Arena.
        var margin = Math.Sqrt(Math.Log(40) / (2.0 * seeds));
        return new(opponent.ToString().ToLowerInvariant(), firstSeed, seeds, first, second, unfinished, rate, Math.Max(0, rate - margin), Math.Min(1, rate + margin));
    }
}
