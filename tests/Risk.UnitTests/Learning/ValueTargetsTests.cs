using AwesomeAssertions;
using Risk.Learning;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class ValueTargetsTests
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
    public void LambdaReturns_NoAfterstates_ReturnsNothing()
    {
        ValueTargets.LambdaReturns([], 1, .7).Should().BeEmpty();
    }
}
