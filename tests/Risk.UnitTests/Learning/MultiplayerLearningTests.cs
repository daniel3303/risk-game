using AwesomeAssertions;
using Risk.Learning;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class MultiplayerLearningTests
{
    [Fact]
    public void Extract_ThreePlayerPosition_EncodesShapeOwnershipAndScore()
    {
        var game = TestWorld.Game(3, new(), 57);
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Ultimate);
        var board = PlannerBoard.From(observation);
        var encoding = BoardEncoding.Multiplayer;
        encoding.Supports(evaluator).Should().BeTrue();
        BoardEncoding.Duel.Supports(evaluator).Should().BeFalse();
        var x = new float[encoding.Count];
        encoding.Extract(evaluator, board, 25, x);
        x.Should().OnlyContain(v => float.IsFinite(v));
        var owned = Enumerable.Range(0, 42).Where(id => board.Owners[id] == observation.Player).ToArray();
        Enumerable.Range(0, 42).Sum(id => x[id * 12]).Should().Be(owned.Length);
        Enumerable.Range(0, 42).Sum(id => x[id * 12 + 1] + x[id * 12 + 2]).Should().Be(42 - owned.Length);
        // Every own territory is either a border or interior, and the global counts agree with the per-territory flags.
        var borders = owned.Count(id => observation.Map.Territories[id].Neighbors.Any(n => board.Owners[n] != observation.Player));
        Enumerable.Range(0, 42).Sum(id => x[id * 12 + 9]).Should().Be(borders);
        x[encoding.ScoreIndex].Should().Be(.5f);
        x[encoding.ScoreIndex + 1 + 3 + 3 + 3 + 6 + 6 + 1 + 1 + 24].Should().BeApproximately(borders / 42f, 1e-6f);
        x[encoding.ScoreIndex + 1 + 3 + 3 + 3 + 6 + 6 + 1 + 1 + 24 + 1].Should().BeApproximately((owned.Length - borders) / 42f, 1e-6f);
    }

    [Fact]
    public void Extract_CompleteContinent_FlagsItsGatesAndTheContinent()
    {
        var game = TestWorld.Game(3);
        TestWorld.Board(game);
        // Player 0 holds all of Australia (38-41); Indonesia (38) is its only gate.
        foreach (var id in new[] { 38, 39, 40, 41 }) { game.State.Territories[id].Owner = 0; game.State.Territories[id].Troops = 3; }
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Ultimate);
        var x = new float[BoardEncoding.Multiplayer.Count];
        BoardEncoding.Multiplayer.Extract(evaluator, PlannerBoard.From(observation), 0, x);
        x[38 * 12 + 10].Should().Be(1);
        x[39 * 12 + 10].Should().Be(0);
        var continents = BoardEncoding.Multiplayer.ScoreIndex + 1 + 3 + 3 + 3 + 6 + 6 + 1 + 1;
        var australia = Array.FindIndex(observation.Map.Continents, c => c.Id == "australia");
        x[continents + australia].Should().Be(1);
        x[continents + 12 + australia].Should().Be(1);
    }

    [Fact]
    public void Extract_RivalDownToOneTerritory_EncodesItsCardsAndHowCapturableItIs()
    {
        var game = TestWorld.Game(3);
        TestWorld.Board(game);
        // Player 1 holds only territory 1 with four cards; player 0's stack of ten on territory 0 is adjacent to it.
        game.State.Territories[0].Troops = 10;
        game.State.Players[1].Cards.AddRange([7, 8, 9, 10]);
        var observation = GameObservation.From(game);
        var x = new float[BoardEncoding.Multiplayer.Count];
        BoardEncoding.Multiplayer.Extract(new PositionEvaluator(observation, ExpertTuning.Ultimate), PlannerBoard.From(observation), 0, x);
        var elimination = BoardEncoding.Multiplayer.Count - 10;
        x[elimination].Should().BeApproximately(1 / 42f, 1e-6f);
        x[elimination + 2 + 4].Should().Be(1);
        x[elimination + 8].Should().BeApproximately((float)BattleOdds.Estimate(9, 1).WinChance, 1e-6f);
        x[elimination + 9].Should().Be(x[elimination + 8]);
    }

    [Fact]
    public void Correction_UltimateValuation_RoutesByPlayerCount()
    {
        var zero = new ValueModel { Features = BoardEncoding.Multiplayer.Version, Members = [new ValueNetwork { Inputs = BoardEncoding.Multiplayer.Count, Hidden = 2, A = 1, W = new float[BoardEncoding.Multiplayer.Count * 2], C = new float[2], V = new float[2] }] };
        var biased = new ValueModel { Features = BoardEncoding.Multiplayer.Version, Members = [new ValueNetwork { Inputs = BoardEncoding.Multiplayer.Count, Hidden = 1, A = 1, W = new float[BoardEncoding.Multiplayer.Count], C = [1f], V = [1f] }] };
        var duel = GameObservation.From(TestWorld.Game(2, new(), 58));
        var trio = GameObservation.From(TestWorld.Game(3, new(), 58));
        var duelBoard = PlannerBoard.From(duel);
        var trioBoard = PlannerBoard.From(trio);
        var duelEvaluator = new PositionEvaluator(duel, ExpertTuning.Ultimate);
        var trioEvaluator = new PositionEvaluator(trio, ExpertTuning.Ultimate);
        var composite = new UltimateValuation(ValueModel.Ultimate, biased);
        composite.Correction(duelEvaluator, duelBoard, 0).Should().Be(ValueModel.Ultimate.Correction(duelEvaluator, duelBoard, 0));
        // The biased network's term is exactly one logit, so its correction is the score scale over its calibration.
        composite.Correction(trioEvaluator, trioBoard, 0).Should().BeApproximately(BoardEncoding.ScoreScale, 1e-9);
        new UltimateValuation(ValueModel.Ultimate, zero).Correction(trioEvaluator, trioBoard, 0).Should().Be(0);
        new UltimateValuation(ValueModel.Ultimate, null).Correction(trioEvaluator, trioBoard, 0).Should().Be(0);
    }

    [Fact]
    public void Encode_MultiplayerRow_RebuildsTheSameEvaluatorBoardAndScore()
    {
        var game = TestWorld.Game(4, new(), 59);
        game.State.Players[1].Eliminated = true;
        game.State.Players[2].Cards.AddRange([1, 2, 3]);
        game.State.Players[0].Cards.AddRange([4, 5]);
        game.State.Trades = 2;
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Ultimate);
        var board = PlannerBoard.From(observation);
        board.Conquered = true;
        var format = TrajectoryFormat.Multiplayer;
        var row = format.Encode(observation, board, evaluator.HandScore(board));
        row.Should().HaveCount(format.Width);
        var (rebuilt, copy) = format.Rebuild(TestWorld.Map(), row);
        copy.Owners.Should().Equal(board.Owners);
        copy.Troops.Should().Equal(board.Troops);
        copy.Conquered.Should().BeTrue();
        rebuilt.Player.Should().Be(0);
        rebuilt.Observation.Cards.Should().HaveCount(2);
        rebuilt.Observation.Players.Should().BeEquivalentTo(observation.Players);
        rebuilt.Observation.Trades.Should().Be(2);
        // The row keeps the score as a single-precision float; training recomputes it exactly from the rebuilt board.
        rebuilt.HandScore(copy).Should().BeApproximately(row[format.Score], 1e-4);
        rebuilt.HandScore(copy).Should().Be(evaluator.HandScore(board));
        var expected = new float[BoardEncoding.Multiplayer.Count];
        var actual = new float[BoardEncoding.Multiplayer.Count];
        BoardEncoding.Multiplayer.Extract(evaluator, board, evaluator.HandScore(board), expected);
        BoardEncoding.Multiplayer.Extract(rebuilt, copy, rebuilt.HandScore(copy), actual);
        actual.Should().Equal(expected);
    }

    [Fact]
    public void Play_ThreePlayerLeagueGame_RecordsOnlyLearnerSeatsWhileThreeRemain()
    {
        var format = TrajectoryFormat.Multiplayer;
        // Seed 1000001 seats a turtle; seed 1000000 has no league seat.
        var (policy, seat) = TrajectoryRecorder.League(3, 1000001);
        policy.Should().Be("turtle");
        TrajectoryRecorder.League(3, 1000000).Policy.Should().BeNull();
        var rows = TrajectoryRecorder.Play(TestWorld.Map(), 3, 1000001, null);
        rows.Should().NotBeEmpty();
        (rows.Length % format.Width).Should().Be(0);
        var records = Enumerable.Range(0, rows.Length / format.Width).Select(r => rows.AsSpan(r * format.Width, format.Width).ToArray()).ToArray();
        records.Should().OnlyContain(r => (int)r[format.Mover] != seat && r[format.Seed] == 1000001);
        records.Should().OnlyContain(r => r[format.Outcome] == 0 || r[format.Outcome] == 1 || r[format.Outcome] == .5f);
        foreach (var mover in records.GroupBy(r => (int)r[format.Mover]))
        {
            mover.Select(r => (int)r[format.Turn]).Should().Equal(Enumerable.Range(0, mover.Count()));
            mover.Select(r => r[format.Outcome]).Distinct().Should().ContainSingle();
        }
    }

    [Theory]
    [InlineData("artifacts/learning/gen3", "artifacts/learning/gen4")]
    [InlineData("artifacts/learning/gen9/", "artifacts/learning/gen10")]
    [InlineData("artifacts/learning/run", "artifacts/learning/run-2")]
    [InlineData("artifacts/learning/run-2", "artifacts/learning/run-3")]
    [InlineData("artifacts/2026", "artifacts/2026-2")]
    public void NextOutput_GenerationDirectory_CountsUp(string output, string next) =>
        Improvement.NextOutput(output).Should().Be(next);

    [Fact]
    public void NextSeed_Generation_StartsAfterItsRecordedStrengthAndProbeSeeds()
    {
        var options = new Improvement.Options(3, 1021300, 20000, "gen2", null, 64, 8, .5, 60, 1000, 300, null, 1);
        Improvement.NextSeed(options).Should().Be(1042600);
    }

    [Fact]
    public void Generations_OneGameWithAWindow_FitsBothRecordingsAndGatesAgainstTheCurrentModel()
    {
        var root = Path.Combine(Path.GetTempPath(), $"risk-improve-{Guid.NewGuid():N}");
        try
        {
            var map = TestWorld.Map();
            var format = TrajectoryFormat.Multiplayer;
            var earlier = Path.Combine(root, "earlier.bin");
            Directory.CreateDirectory(root);
            // An earlier generation's recording: one opening afterstate from each of two games.
            var rows = new[] { 5000, 5001 }.SelectMany(seed =>
            {
                var observation = GameObservation.From(TestWorld.Game(3, new(), seed));
                var board = PlannerBoard.From(observation);
                var row = format.Encode(observation, board, new PositionEvaluator(observation, ExpertTuning.Ultimate).HandScore(board));
                row[format.Seed] = seed; row[format.Outcome] = seed % 2; row[format.Turn] = 0;
                return row;
            }).ToArray();
            var bytes = new byte[rows.Length * sizeof(float)];
            Buffer.BlockCopy(rows, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(earlier, bytes);
            var options = new Improvement.Options(3, 6000, 1, Path.Combine(root, "gen1"), null, 2, 1, .5, 60, 1, 1, null, 3, ExtraData: [earlier]);
            var reports = Improvement.Generations(map, options, 2, 2, TextWriter.Null);
            // One seed cannot clear the conservative bound, so the run stops after its first, rejected generation.
            var report = reports.Should().ContainSingle().Subject;
            report.Promoted.Should().BeFalse();
            report.Data.Should().HaveCount(2).And.Contain(earlier);
            report.Afterstates.Should().Be(TrajectoryFormat.Multiplayer.Read(report.Data).Length);
            report.AgainstCurrent.Opponent.Should().Be("current");
            report.AgainstCurrent.FirstSeed.Should().Be(6001);
            report.ProbeAgainstCandidate.FirstSeed.Should().Be(6002);
            File.Exists(Path.Combine(root, "gen1", "improve.json")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "gen2")).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Run_FinishedGeneration_IsNotOverwritten()
    {
        var output = Path.Combine(Path.GetTempPath(), $"risk-improve-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(Path.Combine(output, "improve.json"), "{}");
            var options = new Improvement.Options(3, 0, 1, output, ValueModel.UltimateMultiplayer, 4, 1, .5, 60, 1, 1, null, 1);
            var run = () => Improvement.Run(TestWorld.Map(), options, TextWriter.Null);
            run.Should().Throw<InvalidOperationException>();
            Directory.GetFiles(output).Should().ContainSingle();
        }
        finally { Directory.Delete(output, true); }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void IsValidation_MultiplayerSeeds_HoldsOutATenthOfEveryLeagueOpponentInEverySeat(int players)
    {
        var format = (MultiplayerTrajectoryFormat)TrajectoryFormat.Multiplayer;
        float[] Row(int seed) { var row = new float[format.Width]; row[format.Seed] = seed; row[format.PlayerCount] = players; return row; }
        var seeds = Enumerable.Range(1021300, 40 * 4 * players * 10).ToArray();
        var held = seeds.Where(s => format.IsValidation(Row(s))).ToArray();
        held.Should().HaveCount(seeds.Length / 10);
        // Each held-out game's league opponent and seat, exactly as the recorder assigns them.
        var cells = held.Select(s => TrajectoryRecorder.League(players, s)).Where(l => l.Policy != null).GroupBy(l => l).Select(g => g.Count()).ToArray();
        cells.Should().HaveCount(3 * players).And.OnlyContain(c => c == cells[0]);
    }

    [Fact]
    public void IsValidation_DuelSeeds_KeepEveryTenthSeed()
    {
        var row = new float[TrajectoryFormat.Duel.Width];
        row[TrajectoryFormat.Duel.Seed] = 1000010;
        TrajectoryFormat.Duel.IsValidation(row).Should().BeTrue();
        row[TrajectoryFormat.Duel.Seed] = 1000004;
        TrajectoryFormat.Duel.IsValidation(row).Should().BeFalse();
    }

    [Fact]
    public void Run_ModelEncodingMismatch_IsRejected()
    {
        var run = () => ModelEvaluator.Run(TestWorld.Map(), ValueModel.Ultimate, "master", 0, 1, 3, false, 1);
        run.Should().Throw<ArgumentException>();
    }
}
