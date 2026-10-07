using System.Diagnostics;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>Self-play two-player games; records the mover's board each time it ends its attacks, then the final outcome.</summary>
public static class TrajectoryRecorder
{
    public static void Record(WorldMap map, int firstSeed, int games, string output, ValueModel model, int threads, TextWriter log)
    {
        var watch = Stopwatch.StartNew();
        var done = Recorded(output, out var dropped);
        var seeds = Enumerable.Range(firstSeed, games).Where(seed => !done.Contains(seed)).ToList();
        // The dropped last game is replayed even outside this range, so appending a new range to a file loses nothing.
        if (dropped is int last && !seeds.Contains(last)) seeds.Insert(0, last);
        using var stream = new FileStream(output, FileMode.Append, FileAccess.Write);
        var writeLock = new object();
        int played = 0, rows = 0;
        // Each game is appended as soon as it ends, so an interrupted run loses at most the games still in progress.
        Parallel.ForEach(seeds, new ParallelOptions { MaxDegreeOfParallelism = threads }, seed =>
        {
            var result = Play(map, seed, model);
            var bytes = new byte[result.Length * sizeof(float)];
            Buffer.BlockCopy(result, 0, bytes, 0, bytes.Length);
            lock (writeLock)
            {
                stream.Write(bytes);
                stream.Flush();
                rows += result.Length / TrajectoryFormat.Width;
                if (++played % 1000 == 0) log.WriteLine($"{played} games, {rows} afterstates, {watch.Elapsed.TotalMinutes:F0} min");
            }
        });
        log.WriteLine($"Recorded {played} games ({done.Count} already saved) and {rows} afterstates in {watch.Elapsed.TotalSeconds:F0} s.");
    }

    public static float[] Play(WorldMap map, int seed, ValueModel model)
    {
        var game = new Game(map, ["A", "B"], new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
        IPlayerStrategy strategy = model == null ? new MasterStrategy() : new ExpertStrategy(ExpertTuning.Master with { Valuation = model });
        var pending = new List<(int Player, float[] Row)>();
        var turns = new int[2];
        var actions = 0;
        while (game.State.Phase != Phase.Finished && game.State.Round <= 200 && actions++ < 20000)
        {
            var player = game.State.CurrentPlayer;
            var observation = GameObservation.From(game);
            var command = strategy.Choose(observation);
            if (command.Kind == CommandKind.EndAttack)
            {
                var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
                var board = PlannerBoard.From(observation);
                if (evaluator.Duel)
                {
                    var row = TrajectoryFormat.Encode(observation, board, evaluator.Evaluate(board));
                    row[TrajectoryFormat.Turn] = turns[player]++;
                    row[TrajectoryFormat.Mover] = player;
                    pending.Add((player, row));
                }
            }
            game.Apply(player, command);
        }
        var result = new float[pending.Count * TrajectoryFormat.Width];
        for (var i = 0; i < pending.Count; i++)
        {
            var row = pending[i].Row;
            row[TrajectoryFormat.Outcome] = game.State.Phase != Phase.Finished ? .5f : game.State.Winner == pending[i].Player ? 1 : 0;
            row[TrajectoryFormat.Seed] = seed;
            row.CopyTo(result, i * TrajectoryFormat.Width);
        }
        return result;
    }

    /// <summary>
    /// Seeds already saved. Each game is written in one piece, so only the last game can be partial after an interruption;
    /// its rows are removed and its seed is returned as <paramref name="dropped"/> to be played again.
    /// </summary>
    public static HashSet<int> Recorded(string output, out int? dropped)
    {
        var done = new HashSet<int>();
        dropped = null;
        if (!File.Exists(output)) return done;
        var rows = TrajectoryFormat.Read([output]);
        if (rows.Length > 0) dropped = (int)rows[^1][TrajectoryFormat.Seed];
        var keep = rows.Length;
        while (keep > 0 && rows[keep - 1][TrajectoryFormat.Seed] == rows[^1][TrajectoryFormat.Seed]) keep--;
        // An empty file, or one holding only a partial row, simply starts over.
        using (var trim = new FileStream(output, FileMode.Open)) trim.SetLength((long)keep * TrajectoryFormat.Width * sizeof(float));
        for (var r = 0; r < keep; r++) done.Add((int)rows[r][TrajectoryFormat.Seed]);
        return done;
    }
}
