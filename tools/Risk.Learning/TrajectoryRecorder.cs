using System.Diagnostics;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// Self-play games that record the learner's board each time it ends its attacks, then the final outcome. Duels replay Master
/// (or Master with a duel model); larger games seat the current Ultimate, with one seat in every fourth game given to a league
/// opponent (turtle, Master or Expert) so the learned value also sees human-style and weaker play.
/// </summary>
public static class TrajectoryRecorder
{
    public static void Record(WorldMap map, int firstSeed, int games, string output, ValueModel model, int threads, TextWriter log) =>
        Record(map, 2, firstSeed, games, output, model, threads, log);

    public static void Record(WorldMap map, int players, int firstSeed, int games, string output, ValueModel model, int threads, TextWriter log)
    {
        var format = TrajectoryFormat.For(players);
        var watch = Stopwatch.StartNew();
        var done = Recorded(output, format, out var dropped);
        var seeds = Enumerable.Range(firstSeed, games).Where(seed => !done.Contains(seed)).ToList();
        // The dropped last game is replayed even outside this range, so appending a new range to a file loses nothing.
        if (dropped is int last && !seeds.Contains(last)) seeds.Insert(0, last);
        using var stream = new FileStream(output, FileMode.Append, FileAccess.Write);
        var writeLock = new object();
        int played = 0, rows = 0;
        // Each game is appended as soon as it ends, so an interrupted run loses at most the games still in progress.
        Parallel.ForEach(seeds, new ParallelOptions { MaxDegreeOfParallelism = threads }, seed =>
        {
            var result = Play(map, players, seed, model);
            var bytes = new byte[result.Length * sizeof(float)];
            Buffer.BlockCopy(result, 0, bytes, 0, bytes.Length);
            lock (writeLock)
            {
                stream.Write(bytes);
                stream.Flush();
                rows += result.Length / format.Width;
                if (++played % 1000 == 0) log.WriteLine($"{played} games, {rows} afterstates, {watch.Elapsed.TotalMinutes:F0} min");
            }
        });
        log.WriteLine($"Recorded {played} games ({done.Count} already saved) and {rows} afterstates in {watch.Elapsed.TotalSeconds:F0} s.");
    }

    public static float[] Play(WorldMap map, int seed, ValueModel model) => Play(map, 2, seed, model);

    /// <summary>The league seat of a game: none in every fourth game, otherwise a turtle, Master or Expert in a rotating seat.</summary>
    public static (string Policy, int Seat) League(int players, int seed) => (seed % 4) switch
    {
        1 => ("turtle", seed / 4 % players),
        2 => ("master", seed / 4 % players),
        3 => ("expert", seed / 4 % players),
        _ => (null, -1),
    };

    public static float[] Play(WorldMap map, int players, int seed, ValueModel model)
    {
        var format = TrajectoryFormat.For(players);
        var game = new Game(map, Enumerable.Range(0, players).Select(i => ((char)('A' + i)).ToString()).ToArray(), new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
        var strategies = Strategies(players, seed, model, out var leagueSeat);
        var tuning = players == 2 ? ExpertTuning.Master : ExpertTuning.Ultimate;
        var pending = new List<(int Player, float[] Row)>();
        var turns = new int[players];
        var actions = 0;
        while (game.State.Phase != Phase.Finished && game.State.Round <= 200 && actions++ < 20000)
        {
            var player = game.State.CurrentPlayer;
            var observation = GameObservation.From(game);
            var command = strategies[player].Choose(observation);
            if (command.Kind == CommandKind.EndAttack && player != leagueSeat)
            {
                var evaluator = new PositionEvaluator(observation, tuning);
                var board = PlannerBoard.From(observation);
                if (format.Encoding.Supports(evaluator))
                {
                    var row = format.Encode(observation, board, evaluator.HandScore(board));
                    row[format.Turn] = turns[player]++;
                    row[format.Mover] = player;
                    pending.Add((player, row));
                }
            }
            game.Apply(player, command);
        }
        var result = new float[pending.Count * format.Width];
        for (var i = 0; i < pending.Count; i++)
        {
            var row = pending[i].Row;
            row[format.Outcome] = game.State.Phase != Phase.Finished ? .5f : game.State.Winner == pending[i].Player ? 1 : 0;
            row[format.Seed] = seed;
            row.CopyTo(result, i * format.Width);
        }
        return result;
    }

    /// <summary>Learners in every seat except the league seat (-1 when there is none); duel learners are Master with an optional duel model.</summary>
    private static IPlayerStrategy[] Strategies(int players, int seed, ValueModel model, out int leagueSeat)
    {
        IPlayerStrategy learner = players == 2
            ? model == null ? new MasterStrategy() : new ExpertStrategy(ExpertTuning.Master with { Valuation = model })
            : new ExpertStrategy(ExpertTuning.Ultimate with { Valuation = new UltimateValuation(ValueModel.Ultimate, model ?? ValueModel.UltimateMultiplayer) });
        var (policy, seat) = players == 2 ? (null, -1) : League(players, seed);
        leagueSeat = seat;
        return Enumerable.Range(0, players).Select(i => i == seat ? PolicyCatalog.Create(policy, unchecked(seed * 1000003 + i * 7919)) : learner).ToArray();
    }

    public static HashSet<int> Recorded(string output, out int? dropped) => Recorded(output, TrajectoryFormat.Duel, out dropped);

    /// <summary>
    /// Seeds already saved. Each game is written in one piece, so only the last game can be partial after an interruption;
    /// its rows are removed and its seed is returned as <paramref name="dropped"/> to be played again.
    /// </summary>
    public static HashSet<int> Recorded(string output, TrajectoryFormat format, out int? dropped)
    {
        var done = new HashSet<int>();
        dropped = null;
        if (!File.Exists(output)) return done;
        var rows = format.Read([output]);
        if (rows.Length > 0) dropped = (int)rows[^1][format.Seed];
        var keep = rows.Length;
        while (keep > 0 && rows[keep - 1][format.Seed] == rows[^1][format.Seed]) keep--;
        // An empty file, or one holding only a partial row, simply starts over.
        using (var trim = new FileStream(output, FileMode.Open)) trim.SetLength((long)keep * format.Width * sizeof(float));
        for (var r = 0; r < keep; r++) done.Add((int)rows[r][format.Seed]);
        return done;
    }
}
