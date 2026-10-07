using AwesomeAssertions;
using Risk.Learning;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class LuckAnalysisTests
{
    [Fact]
    public void Run_SameSeeds_IsRepeatableAndSplitsTheVariance()
    {
        var first = LuckAnalysis.Run(TestWorld.Map(), 920000, 3, 3, 2);
        var second = LuckAnalysis.Run(TestWorld.Map(), 920000, 3, 3, 1);
        second.Should().Be(first);
        (first.DealVariance + first.DiceVariance).Should().BeApproximately(first.OutcomeVariance, 1e-12);
        first.DiceVariance.Should().BeInRange(0, first.OutcomeVariance);
    }
}
