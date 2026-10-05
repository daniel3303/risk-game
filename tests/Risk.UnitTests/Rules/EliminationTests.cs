using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class EliminationTests
{
    [Fact]
    public void Apply_EliminationDraft_TradesInheritedCardsThenResumesWithoutNewBaseTroops()
    {
        var game = TestWorld.Game(3, new(CardMode.Progressive));
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 8;
        game.State.Territories[1].Troops = 0;
        game.State.Capture = new(0, 1, 3, 7);
        game.State.Phase = Phase.Occupy;
        game.State.Players[0].Cards.AddRange([3, 6, 9]);
        game.State.Players[1].Cards.AddRange([12, 15, 18]);
        game.Apply(0, new() { Kind = CommandKind.Occupy, Count = 7 });
        game.State.Reinforcements.Should().Be(0);
        game.Apply(0, new() { Kind = CommandKind.Trade, Cards = [3, 6, 9] });
        game.State.Reinforcements.Should().Be(4);
        game.Apply(0, new() { Kind = CommandKind.Place, To = 1, Count = 4 });
        game.State.Phase.Should().Be(Phase.Attack);
        game.State.CurrentPlayer.Should().Be(0);
        game.State.Territories[1].Troops.Should().Be(11);
    }

    [Fact]
    public void Apply_Surrender_SkipsEliminatedPlayerAndRejectsTheirCommands()
    {
        var game = TestWorld.Game();
        game.Apply(0, new() { Kind = CommandKind.Surrender });
        game.State.CurrentPlayer.Should().Be(1);
        game.State.Players[0].Eliminated.Should().BeTrue();
        Action act = () => game.Apply(0, new() { Kind = CommandKind.EndTurn });
        act.Should().Throw<RuleException>();
    }
}
