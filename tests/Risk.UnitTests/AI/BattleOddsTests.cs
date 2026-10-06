using AwesomeAssertions;
using Risk.Sim.AI.Planning;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class BattleOddsTests
{
    [Fact]
    public void Round_ThreeVersusTwo_EnumeratesAllDiceOutcomesWithDefenderWinningTies()
    {
        var distribution = BattleOdds.Round(3, 2);
        distribution.Sum(p => p.Probability).Should().BeApproximately(1, 1e-12);
        distribution.Single(p => p.DefenderLosses == 2).Probability.Should().BeApproximately(2890.0 / 7776, 1e-12);
        distribution.Single(p => p.AttackerLosses == 2).Probability.Should().BeApproximately(2275.0 / 7776, 1e-12);
    }

    [Fact]
    public void Estimate_SmallBattles_UsesExactAbsorbingProbabilities()
    {
        var one = BattleOdds.Estimate(1, 1);
        one.WinChance.Should().BeApproximately(15.0 / 36, 1e-12);
        one.SurvivorsOnWin.Should().BeApproximately(1, 1e-12);
        one.DefendersOnLoss.Should().BeApproximately(1, 1e-12);
        BattleOdds.Estimate(2, 1).WinChance.Should().BeApproximately(1955.0 / 2592, 1e-12);
        BattleOdds.Estimate(0, 5).WinChance.Should().Be(0);
        BattleOdds.Estimate(5, 0).SurvivorsOnWin.Should().Be(5);
    }

    [Fact]
    public void Estimate_ArmyAdvantages_IncreaseVictoryChance()
    {
        var weak = BattleOdds.Estimate(8, 12);
        var strong = BattleOdds.Estimate(18, 12);
        strong.WinChance.Should().BeGreaterThan(weak.WinChance);
        strong.SurvivorsOnWin.Should().BeInRange(1, 18);
        strong.DefendersOnLoss.Should().BeInRange(1, 12);
    }

    [Fact]
    public void Estimate_LargeProgressiveArmies_KeepsAbsoluteArmyScale()
    {
        var small = BattleOdds.Estimate(90, 100);
        var large = BattleOdds.Estimate(9000, 10000);
        large.WinChance.Should().BeGreaterThan(small.WinChance);
        large.WinChance.Should().BeInRange(0, 1);
        large.SurvivorsOnWin.Should().BeInRange(1, 9000);
        large.DefendersOnLoss.Should().BeInRange(1, 10000);
        BattleOdds.Estimate(1000, 5000).WinChance.Should().BeLessThan(.001);
    }
}
