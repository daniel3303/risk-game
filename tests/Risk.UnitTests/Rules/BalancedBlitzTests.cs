using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Risk.Sim.Rules;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class BalancedBlitzTests
{
    // Reference values from the SMG bridge's port, itself checked against an independent Python port of the client.
    private const double Tolerance = 1e-12;

    [Fact]
    public void Calculate_ThreeVersusTwo_MatchesExactBlitz()
    {
        var (attack, defend) = BlitzCalculator.Calculate(3, 2);

        AssertClose([0.37165637860082307, 0.1943152482472184, 0.08998237295508817, 0.34404600019687037], attack);
        AssertClose([0.21807067805974698, 0.12597532213712342, 0.6559539998031296], defend);
    }

    [Fact]
    public void Odds_ThreeVersusTwo_PushesTheFavouriteFrom66To75Percent()
    {
        var (attack, defend) = BalancedBlitz.Odds(3, 2);

        AssertClose([0.4511980666636825, 0.2372518688099024, 0.059344526114690724, 0.25220553841172433], attack);
        AssertClose([0.10462286289706325, 0.14758267551466112, 0.7477944615882757], defend);
    }

    [Fact]
    public void Odds_TenVersusTen_TrimsBothTails()
    {
        var (attack, defend) = BalancedBlitz.Odds(10, 10);

        AssertClose([0, 0, 0, 0.017962471829092764, 0.09922270592367605, 0.12476869540228011, 0.14841860176761767,
            0.13857378005824975, 0.062367056001857164, 0.018064055065527627, 0.3906226339516988], attack);
        AssertClose([0, 0, 0, 0, 0.0013173719355738175, 0.0588056437202505, 0.0891824090732293, 0.09260287527809928,
            0.11403222704792558, 0.0346821068966203, 0.6093773660483011], defend);
    }

    [Fact]
    public void Odds_AttackerBelowFivePercent_NeverWins()
    {
        var (attackLoss, defendLoss) = BalancedBlitz.Odds(1, 10);

        defendLoss[10].Should().Be(0);
        attackLoss[1].Should().Be(1);
    }

    [Fact]
    public void Odds_EverySmallSize_SidesAgreeAndSumToOne()
    {
        for (var attack = 1; attack <= 12; attack++)
        {
            for (var defend = 1; defend <= 12; defend++)
            {
                var (attackLoss, defendLoss) = BalancedBlitz.Odds(attack, defend);
                attackLoss.Sum().Should().BeApproximately(1, 1e-9);
                defendLoss.Sum().Should().BeApproximately(1, 1e-9);
                attackLoss[..attack].Sum().Should().BeApproximately(defendLoss[defend], 1e-9);
                attackLoss.Concat(defendLoss).Should().OnlyContain(p => p >= 0);
            }
        }
    }

    [Theory]
    [InlineData(0.0, 0, 2)]
    [InlineData(0.5, 1, 2)]
    [InlineData(0.75, 3, 0)]
    [InlineData(1.0, 3, 1)]
    public void Resolve_ThreeVersusTwo_PicksOutcomeByUniform(double uniform, int attackerLosses, int defenderLosses) =>
        BalancedBlitz.Resolve(3, 2, uniform).Should().Be((attackerLosses, defenderLosses));

    [Fact]
    public void NextUniform_CombinesTwoDraws()
    {
        BalancedBlitz.NextUniform(new SequenceRandom([0, 0])).Should().Be(0);
        BalancedBlitz.NextUniform(new SequenceRandom([1 << 25, 0])).Should().Be(.5);
        BalancedBlitz.NextUniform(new SequenceRandom([(1 << 26) - 1, (1 << 27) - 1])).Should().BeLessThan(1);
    }

    [Fact]
    public void Attack_BalancedBlitz_SettlesTheWholeBattleFromOneUniform()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        var from = game.State.Territories[0]; from.Troops = 4;
        var to = game.State.Territories[1]; to.Troops = 2;

        // Uniform 0.5 on 3 v 2: the attacker loses one and takes the territory.
        var result = CombatRules.Attack(game.State, from, to, new() { Count = 3 }, new SequenceRandom([1 << 25, 0]), DiceMode.BalancedBlitz);

        result.AttackerLosses.Should().Be(1);
        result.DefenderLosses.Should().Be(2);
        result.AttackDice.Should().BeEmpty();
        from.Troops.Should().Be(3);
        to.Troops.Should().Be(0);
        game.State.Phase.Should().Be(Phase.Occupy);
        game.State.Capture.Minimum.Should().Be(2);
    }

    [Fact]
    public void Attack_BalancedBlitzLoss_LeavesTheDefenderItsSurvivors()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        var from = game.State.Territories[0]; from.Troops = 4;
        var to = game.State.Territories[1]; to.Troops = 2;

        var result = CombatRules.Attack(game.State, from, to, new() { Count = 3 }, new SequenceRandom([(1 << 26) - 1, (1 << 27) - 1]), DiceMode.BalancedBlitz);

        result.AttackerLosses.Should().Be(3);
        result.DefenderLosses.Should().Be(1);
        from.Troops.Should().Be(1);
        to.Troops.Should().Be(1);
        game.State.Phase.Should().Be(Phase.Attack);
    }

    [Fact]
    public void Apply_BalancedBlitzGame_FightsEveryAttackToTheEnd()
    {
        var game = TestWorld.Game(options: new(CardMode.Fixed, SetupMode.Automatic, DiceMode.BalancedBlitz));
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 9;
        game.State.Territories[1].Troops = 6;

        game.Apply(0, new() { Kind = CommandKind.Attack, From = 0, To = 1 });

        (game.State.Territories[0].Troops == 1 || game.State.Territories[1].Troops == 0).Should().BeTrue("a balanced blitz never stops part way");
    }

    private static void AssertClose(double[] expected, double[] actual)
    {
        actual.Should().HaveCount(expected.Length);
        for (var i = 0; i < expected.Length; i++) actual[i].Should().BeApproximately(expected[i], Tolerance);
    }
}
