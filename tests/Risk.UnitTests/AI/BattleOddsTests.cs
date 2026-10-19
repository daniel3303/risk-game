using AwesomeAssertions;
using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
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

    [Fact]
    public void Estimate_BalancedBlitzRules_ReadTheBalancedDistribution()
    {
        var balanced = BattleOdds.Estimate(3, 2, new GameOptions(Dice: DiceMode.BalancedBlitz));
        balanced.WinChance.Should().BeApproximately(0.7477944615882757, 1e-12);
        balanced.Should().Be(BattleOdds.Balanced(3, 2));
        BattleOdds.Estimate(3, 2, new GameOptions()).Should().Be(BattleOdds.Estimate(3, 2));
        BattleOdds.Estimate(3, 2, null).Should().Be(BattleOdds.Estimate(3, 2));
    }

    [Fact]
    public void Balanced_ExpectedArmies_WeighEachOutcomeBySurvivors()
    {
        var (attackLoss, defendLoss) = Risk.Sim.Rules.BalancedBlitz.Odds(3, 2);
        var estimate = BattleOdds.Balanced(3, 2);
        estimate.WinningArmies.Should().BeApproximately(attackLoss[0] * 3 + attackLoss[1] * 2 + attackLoss[2], 1e-12);
        estimate.LosingDefenders.Should().BeApproximately(defendLoss[0] * 2 + defendLoss[1], 1e-12);
        BattleOdds.Balanced(5, 0).Should().Be(new BattleEstimate(1, 5, 0));
        BattleOdds.Balanced(0, 5).Should().Be(new BattleEstimate(0, 0, 5));
    }

    [Fact]
    public void Balanced_BattlesTheClientEstimates_KeepTrueRandomOdds() =>
        BattleOdds.Balanced(1999, 2).Should().Be(BattleOdds.Estimate(1999, 2));
}
