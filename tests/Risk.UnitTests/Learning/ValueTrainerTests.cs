using AwesomeAssertions;
using Risk.Learning;
using Risk.Sim.Learning;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class ValueTrainerTests
{
    [Fact]
    public void Fit_DifferentThreadCounts_ProduceIdenticalModelsThatPlayLegally()
    {
        var map = TestWorld.Map();
        var rows = Enumerable.Range(0, 12).SelectMany(seed => TrajectoryRecorder.Play(map, 761000 + seed, null)).ToArray();
        var records = Enumerable.Range(0, rows.Length / TrajectoryFormat.Width).Select(r => rows.AsSpan(r * TrajectoryFormat.Width, TrajectoryFormat.Width).ToArray()).ToArray();
        var single = new ValueTrainer(4, 2, .001, 0, .7, 1, 1, TextWriter.Null).Fit(map, records, null);
        var parallel = new ValueTrainer(4, 2, .001, 0, .7, 1, 3, TextWriter.Null).Fit(map, records, null);
        new ValueModel { Members = [parallel] }.Serialize().Should().Be(new ValueModel { Members = [single] }.Serialize());
        single.W.Should().OnlyContain(w => float.IsFinite(w));
        TrajectoryRecorder.Play(map, 762000, new ValueModel { Members = [single] }).Should().NotBeEmpty();
    }
}
