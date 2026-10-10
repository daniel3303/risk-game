using Risk.Learning.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// Plays a candidate value model against a catalogue opponent or another model, every seed from every seat. A duel model corrects
/// Master's planner; a multiplayer model joins the bundled duel model behind Ultimate's planner. In defend mode the opponent takes
/// the rotating seat against candidates everywhere else, and the reported rate is the opponent's.
/// </summary>
public static class ModelEvaluator
{
    public static EvaluationSummary Run(WorldMap map, ValueModel model, string opponent, int firstSeed, int seeds, int threads) =>
        Run(map, model, opponent, firstSeed, seeds, 2, false, threads);

    public static EvaluationSummary Run(WorldMap map, ValueModel model, string opponent, int firstSeed, int seeds, int players, bool defend, int threads) =>
        Play(map, model, opponent, (seed, i) => PolicyCatalog.Create(opponent, unchecked(seed * 1000003 + i * 7919)), firstSeed, seeds, players, defend, threads);

    /// <summary>Plays the candidate against copies of <paramref name="opponent"/>, reported under <paramref name="label"/>.</summary>
    public static EvaluationSummary Run(WorldMap map, ValueModel model, ValueModel opponent, string label, int firstSeed, int seeds, int players, int threads)
    {
        if (opponent.Encoding != model.Encoding) throw new ArgumentException("The candidate and its opponent must share one board encoding.");
        return Play(map, model, label, (_, _) => Candidate(opponent), firstSeed, seeds, players, false, threads);
    }

    private static EvaluationSummary Play(WorldMap map, ValueModel model, string opponent, Func<int, int, IPlayerStrategy> create, int firstSeed, int seeds, int players, bool defend, int threads)
    {
        if (model.Encoding != BoardEncoding.For(TrajectoryFormat.For(players).Encoding.Version))
            throw new ArgumentException($"A {model.Features} model cannot be evaluated in {players}-player games.");
        var wins = new int[players];
        var unfinished = 0;
        Parallel.For(0, seeds * players, new ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
        {
            var seed = firstSeed + job / players;
            var seat = job % players;
            var game = new Game(map, Enumerable.Range(0, players).Select(i => ((char)('A' + i)).ToString()).ToArray(), new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
            var strategies = Enumerable.Range(0, players).Select(i => (i == seat) != defend ? Candidate(model) : create(seed, i)).ToArray();
            var actions = 0;
            while (game.State.Phase != Phase.Finished && game.State.Round <= 200 && actions++ < 20000)
                game.Apply(game.State.CurrentPlayer, strategies[game.State.CurrentPlayer].Choose(GameObservation.From(game)));
            if (game.State.Phase != Phase.Finished) Interlocked.Increment(ref unfinished);
            else if (game.State.Winner == seat) Interlocked.Increment(ref wins[seat]);
        });
        var rate = wins.Sum() / (double)(seeds * players);
        // Same conservative Hoeffding bound over seed blocks as the Arena.
        var margin = Math.Sqrt(Math.Log(40) / (2.0 * seeds));
        return new(opponent, players, defend, firstSeed, seeds, wins, unfinished, rate, Math.Max(0, rate - margin), Math.Min(1, rate + margin));
    }

    public static IPlayerStrategy Candidate(ValueModel model) => model.Encoding == BoardEncoding.Duel
        ? new ExpertStrategy(ExpertTuning.Master with { Valuation = model })
        : new ExpertStrategy(ExpertTuning.Ultimate with { Valuation = new UltimateValuation(ValueModel.Ultimate, model) });
}
