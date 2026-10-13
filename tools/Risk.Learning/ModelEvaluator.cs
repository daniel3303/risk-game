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
/// the rotating seat against candidates everywhere else, is flagged as the human, and the reported rate is the opponent's.
/// </summary>
public static class ModelEvaluator
{
    public static EvaluationSummary Run(WorldMap map, ValueModel model, string opponent, int firstSeed, int seeds, int threads) =>
        Run(map, model, opponent, firstSeed, seeds, 2, false, threads);

    /// <param name="tuning">The multiplayer candidates' planner with the model as its correction; null plays Ultimate's.</param>
    public static EvaluationSummary Run(WorldMap map, ValueModel model, string opponent, int firstSeed, int seeds, int players, bool defend, int threads, ExpertTuning tuning = null) =>
        Play(map, model, opponent, (seed, i) => PolicyCatalog.Create(opponent, unchecked(seed * 1000003 + i * 7919)), firstSeed, seeds, players, defend, threads, tuning);

    /// <summary>Plays the candidate against copies of <paramref name="opponent"/>, reported under <paramref name="label"/>.</summary>
    public static EvaluationSummary Run(WorldMap map, ValueModel model, ValueModel opponent, string label, int firstSeed, int seeds, int players, int threads)
    {
        if (!TrajectoryFormat.For(players).Accepts(opponent.Encoding))
            throw new ArgumentException($"A {opponent.Features} model cannot play {players}-player games.");
        return Play(map, model, label, (_, _) => Candidate(opponent), firstSeed, seeds, players, false, threads, null);
    }

    private static EvaluationSummary Play(WorldMap map, ValueModel model, string opponent, Func<int, int, IPlayerStrategy> create, int firstSeed, int seeds, int players, bool defend, int threads, ExpertTuning tuning)
    {
        if (!TrajectoryFormat.For(players).Accepts(model.Encoding))
            throw new ArgumentException($"A {model.Features} model cannot be evaluated in {players}-player games.");
        var wins = new int[players];
        var blocks = new int[seeds];
        var unfinished = 0;
        var stalled = new System.Collections.Concurrent.ConcurrentBag<(int Seed, int Seat, string Board)>();
        Parallel.For(0, seeds * players, new ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
        {
            var seed = firstSeed + job / players;
            var seat = job % players;
            var game = new Game(map, Enumerable.Range(0, players).Select(i => ((char)('A' + i)).ToString()).ToArray(), new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
            var strategies = Enumerable.Range(0, players).Select(i => (i == seat) != defend ? Candidate(model, tuning) : create(seed, i)).ToArray();
            int[] humans = defend ? [seat] : null;
            var actions = 0;
            while (game.State.Phase != Phase.Finished && game.State.Round <= 200 && actions++ < 20000)
                game.Apply(game.State.CurrentPlayer, strategies[game.State.CurrentPlayer].Choose(GameObservation.From(game, humans)));
            if (game.State.Phase != Phase.Finished)
            {
                Interlocked.Increment(ref unfinished);
                var board = string.Join(" ", Enumerable.Range(0, players).Select(p =>
                    $"{game.State.Territories.Count(t => t.Owner == p)}t/{game.State.Territories.Where(t => t.Owner == p).Sum(t => t.Troops)}a"));
                stalled.Add((seed, seat, board));
            }
            else if (game.State.Winner == seat) { Interlocked.Increment(ref wins[seat]); Interlocked.Increment(ref blocks[job / players]); }
        });
        var rate = wins.Sum() / (double)(seeds * players);
        // Same conservative Hoeffding bound over seed blocks as the Arena.
        var margin = Math.Sqrt(Math.Log(40) / (2.0 * seeds));
        var (lower, upper) = EvaluationSummary.SeedBlockNormal(blocks, players);
        return new(opponent, players, defend, firstSeed, seeds, wins, unfinished, rate, Math.Max(0, rate - margin), Math.Min(1, rate + margin), lower, upper)
        {
            UnfinishedGames = stalled.OrderBy(g => g.Seed).ThenBy(g => g.Seat).Select(g => $"{g.Seed}/{g.Seat}: {g.Board}").ToList(),
            BlockWins = blocks,
        };
    }

    public static IPlayerStrategy Candidate(ValueModel model, ExpertTuning tuning = null) => model.Encoding == BoardEncoding.Duel
        ? new ExpertStrategy(ExpertTuning.Master with { Valuation = model })
        : new ExpertStrategy((tuning ?? ExpertTuning.Ultimate) with { Valuation = new UltimateValuation(ValueModel.Ultimate, model) });
}
