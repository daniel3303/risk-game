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
        (rows.Length % TrajectoryFormat.Width).Should().Be(0);
        var records = Split(rows);
        records.Should().OnlyContain(r => r[TrajectoryFormat.Seed] == 760000);
        records.Select(r => r[TrajectoryFormat.Outcome]).Distinct().Order().Should().Equal(0f, 1f);
        foreach (var player in records.GroupBy(r => r[TrajectoryFormat.Mover]))
        {
            player.Select(r => (int)r[TrajectoryFormat.Turn]).Should().Equal(Enumerable.Range(0, player.Count()));
            player.Select(r => r[TrajectoryFormat.Outcome]).Distinct().Should().ContainSingle();
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
            TrajectoryRecorder.Recorded(resumed).Should().Equal(763000);
            TrajectoryRecorder.Record(map, 763000, 3, resumed, null, 1, TextWriter.Null);
            File.ReadAllBytes(resumed).Should().Equal(bytes);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static float[][] Split(float[] rows) => Enumerable.Range(0, rows.Length / TrajectoryFormat.Width)
        .Select(r => rows.AsSpan(r * TrajectoryFormat.Width, TrajectoryFormat.Width).ToArray()).ToArray();
}
