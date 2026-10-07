using AwesomeAssertions;
using Risk.Learning;
using Risk.Sim.Learning;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class LearningToolTests
{
    [Fact]
    public void LambdaReturns_ThreeAfterstates_BlendBootstrapValuesTowardTheOutcome()
    {
        var returns = ValueTargets.LambdaReturns([.2, .4, .6], 1, .5);
        returns.Should().HaveCount(3);
        returns[2].Should().Be(1);
        returns[1].Should().BeApproximately(.8, 1e-12);
        returns[0].Should().BeApproximately(.6, 1e-12);
    }

    [Fact]
    public void Play_MasterSelfPlay_RecordsEachPlayersAfterstatesInOrderWithTheOutcome()
    {
        var rows = TrajectoryRecorder.Play(TestWorld.Map(), 760000, null);
        rows.Length.Should().BePositive().And.Subject.Should().Be(rows.Length / TrajectoryFormat.Width * TrajectoryFormat.Width);
        var records = Enumerable.Range(0, rows.Length / TrajectoryFormat.Width).Select(r => rows.AsSpan(r * TrajectoryFormat.Width, TrajectoryFormat.Width).ToArray()).ToArray();
        records.Should().OnlyContain(r => r[TrajectoryFormat.Seed] == 760000);
        records.Select(r => r[TrajectoryFormat.Outcome]).Distinct().Order().Should().Equal(0f, 1f);
        foreach (var player in records.GroupBy(r => r[TrajectoryFormat.Mover]))
        {
            player.Select(r => (int)r[TrajectoryFormat.Turn]).Should().Equal(Enumerable.Range(0, player.Count()));
            player.Select(r => r[TrajectoryFormat.Outcome]).Distinct().Should().ContainSingle();
        }
    }

    [Fact]
    public void Fit_SmallSelfPlaySample_ProducesAModelThatPlaysLegally()
    {
        var map = TestWorld.Map();
        var rows = Enumerable.Range(0, 20).SelectMany(seed => TrajectoryRecorder.Play(map, 761000 + seed, null)).ToArray();
        var records = Enumerable.Range(0, rows.Length / TrajectoryFormat.Width).Select(r => rows.AsSpan(r * TrajectoryFormat.Width, TrajectoryFormat.Width).ToArray()).ToArray();
        var network = new ValueTrainer(4, 1, .001, 0, .7, 1, 2, TextWriter.Null).Fit(map, records, null);
        network.W.Should().OnlyContain(w => float.IsFinite(w));
        var model = new ValueModel { Members = [network] };
        TrajectoryRecorder.Play(map, 762000, model).Should().NotBeEmpty();
    }
}
