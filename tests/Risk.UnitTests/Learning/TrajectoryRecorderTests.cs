using AwesomeAssertions;
using Risk.Learning;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class TrajectoryRecorderTests
{
    [Fact]
    public void Play_MasterSelfPlay_RecordsEachPlayersAfterstatesInOrderWithTheOutcome()
    {
        var rows = TrajectoryRecorder.Play(TestWorld.Map(), 760000, null);
        rows.Should().NotBeEmpty();
        (rows.Length % TrajectoryFormat.Duel.Width).Should().Be(0);
        var records = Split(rows);
        records.Should().OnlyContain(r => r[TrajectoryFormat.Duel.Seed] == 760000);
        records.Select(r => r[TrajectoryFormat.Duel.Outcome]).Distinct().Order().Should().Equal(0f, 1f);
        foreach (var player in records.GroupBy(r => r[TrajectoryFormat.Duel.Mover]))
        {
            player.Select(r => (int)r[TrajectoryFormat.Duel.Turn]).Should().Equal(Enumerable.Range(0, player.Count()));
            player.Select(r => r[TrajectoryFormat.Duel.Outcome]).Distinct().Should().ContainSingle();
        }
    }

    [Fact]
    public void Record_InterruptedMidGame_ReplaysThePartialGameAndMatchesAnUninterruptedRun()
    {
        var directory = Directory.CreateTempSubdirectory("risk-learning-");
        try
        {
            var map = TestWorld.Map();
            var complete = Path.Combine(directory.FullName, "complete.bin");
            var resumed = Path.Combine(directory.FullName, "resumed.bin");
            TrajectoryRecorder.Record(map, 763000, 3, complete, null, 1, TextWriter.Null);
            // Keep the first game and half of the second, cutting through a row.
            var bytes = File.ReadAllBytes(complete);
            var firstGame = TrajectoryRecorder.Play(map, 763000, null).Length * sizeof(float);
            var secondGame = TrajectoryRecorder.Play(map, 763001, null).Length * sizeof(float);
            File.WriteAllBytes(resumed, bytes[..(firstGame + secondGame / 2 + 3)]);
            var log = new StringWriter();
            TrajectoryRecorder.Record(map, 763000, 3, resumed, null, 1, log);
            log.ToString().Should().StartWith("Recorded 2 games (1 already saved)");
            File.ReadAllBytes(resumed).Should().Equal(bytes);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Record_NewSeedRange_ReplaysTheDroppedLastGameOfTheOldRange()
    {
        var directory = Directory.CreateTempSubdirectory("risk-learning-");
        try
        {
            var map = TestWorld.Map();
            var output = Path.Combine(directory.FullName, "data.bin");
            TrajectoryRecorder.Record(map, 763000, 2, output, null, 1, TextWriter.Null);
            TrajectoryRecorder.Record(map, 763005, 1, output, null, 1, TextWriter.Null);
            var expected = new[] { 763000, 763001, 763005 }.SelectMany(seed => TrajectoryRecorder.Play(map, seed, null)).ToArray();
            TrajectoryFormat.Duel.Read([output]).SelectMany(r => r).Should().Equal(expected);
            TrajectoryRecorder.Recorded(output, out var dropped).Should().BeEquivalentTo([763000, 763001]);
            dropped.Should().Be(763005);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static float[][] Split(float[] rows) => Enumerable.Range(0, rows.Length / TrajectoryFormat.Duel.Width)
        .Select(r => rows.AsSpan(r * TrajectoryFormat.Duel.Width, TrajectoryFormat.Duel.Width).ToArray()).ToArray();
}
