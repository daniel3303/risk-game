using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Risk.Sim.Rules;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class CombatRulesTests
{
    [Fact]
    public void Attack_TiedDice_DefenderWinsBothComparisons()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        var from = game.State.Territories[0]; from.Troops = 4;
        var to = game.State.Territories[1]; to.Troops = 2;
        var result = CombatRules.Attack(game.State, from, to, new() { Dice = 3, Blitz = false }, new SequenceRandom([5]));
        result.AttackerLosses.Should().Be(2);
        result.DefenderLosses.Should().Be(0);
        from.Troops.Should().Be(2);
        to.Troops.Should().Be(2);
    }

    [Fact]
    public void Attack_BlitzCommittedTroops_StopBeforeUsingReservedArmies()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        var from = game.State.Territories[0]; from.Troops = 20;
        var to = game.State.Territories[1]; to.Troops = 20;
        var result = CombatRules.Attack(game.State, from, to, new() { Count = 3 }, new SequenceRandom([0]));
        result.AttackerLosses.Should().Be(3);
        from.Troops.Should().Be(17);
        game.State.Phase.Should().Be(Phase.Attack);
    }

    [Fact]
    public void Occupy_CapturedTerritory_EnforcesMinimumAndTransfersEliminatedCards()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        var from = game.State.Territories[0]; from.Troops = 10;
        var to = game.State.Territories[1]; to.Troops = 1;
        game.State.Players[0].Cards.AddRange([3, 4, 5]);
        game.State.Players[1].Cards.AddRange([6, 7, 8]);
        CombatRules.Attack(game.State, from, to, new() { Blitz = false, Dice = 3 }, new SequenceRandom([5, 5, 5, 0]));
        Action insufficient = () => game.Apply(0, new() { Kind = CommandKind.Occupy, Count = 2 });
        insufficient.Should().Throw<RuleException>();
        to.Owner.Should().Be(1);
        game.Apply(0, new() { Kind = CommandKind.Occupy, Count = 9 });
        from.Troops.Should().Be(1);
        to.Owner.Should().Be(0);
        game.State.Players[1].Eliminated.Should().BeTrue();
        game.State.Players[0].Cards.Should().HaveCount(6);
        game.State.Players[1].Cards.Should().BeEmpty();
        game.State.Phase.Should().Be(Phase.Draft);
        game.State.Reinforcements.Should().Be(0);
        game.State.ResumeAttack.Should().BeTrue();
    }

    [Fact]
    public void Occupy_LastOpponent_SetsWinnerAfterTroopsMoved()
    {
        var game = TestWorld.Game(2);
        TestWorld.Board(game, 0);
        var from = game.State.Territories[0]; from.Troops = 4;
        var to = game.State.Territories[1]; to.Troops = 1;
        CombatRules.Attack(game.State, from, to, new() { Blitz = false, Dice = 3 }, new SequenceRandom([5, 5, 5, 0]));
        game.Apply(0, new() { Kind = CommandKind.Occupy, Count = 3 });
        game.State.Phase.Should().Be(Phase.Finished);
        game.State.Winner.Should().Be(0);
        game.State.Territories.Should().OnlyContain(t => t.Owner == 0 && t.Troops > 0);
    }

    [Fact]
    public void Attack_NonAdjacentOrFriendlyOrInvalidDice_RejectsBeforeMutation()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 4;
        foreach (var command in new GameCommand[] {
            new() { Kind = CommandKind.Attack, From = 0, To = 10 },
            new() { Kind = CommandKind.Attack, From = 0, To = 1, Dice = 0 },
            new() { Kind = CommandKind.Attack, From = 0, To = 1, Count = 1, Dice = 3, Blitz = false },
        })
        {
            Action attack = () => game.Apply(0, command);
            attack.Should().Throw<RuleException>();
            game.State.Territories[0].Troops.Should().Be(4);
        }
    }
}
