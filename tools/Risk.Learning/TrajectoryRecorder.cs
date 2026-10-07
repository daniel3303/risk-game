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
        var done = Recorded(output);
        using var stream = new FileStream(output, FileMode.Append, FileAccess.Write);
        var writeLock = new object();
        int played = 0, rows = 0;
        // Each game is appended as soon as it ends, so an interrupted run resumes after its last saved game.
        Parallel.For(firstSeed, firstSeed + games, new ParallelOptions { MaxDegreeOfParallelism = threads }, seed =>
        {
            if (done.Contains(seed)) return;
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

    private static HashSet<int> Recorded(string output)
    {
        var done = new HashSet<int>();
        if (!File.Exists(output)) return done;
        var length = new FileInfo(output).Length;
        var usable = length / (TrajectoryFormat.Width * sizeof(float)) * TrajectoryFormat.Width * sizeof(float);
        // A partially written final row is discarded before appending.
        if (usable != length) using (var trim = new FileStream(output, FileMode.Open)) trim.SetLength(usable);
        foreach (var row in TrajectoryFormat.Read([output])) done.Add((int)row[TrajectoryFormat.Seed]);
        return done;
    }
}
