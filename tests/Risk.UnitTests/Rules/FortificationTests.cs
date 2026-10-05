using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class FortificationTests
{
    [Fact]
    public void Fortify_ConnectedNonAdjacentTerritories_MovesOnceAndEndsTurn()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Phase = Phase.Fortify;
        foreach (var id in new[] { 0, 3, 6 }) game.State.Territories[id].Owner = 0;
        game.State.Territories[0].Troops = 8;
        game.Apply(0, new() { Kind = CommandKind.Fortify, From = 0, To = 6, Count = 7 });
        game.State.Territories[0].Troops.Should().Be(1);
        game.State.Territories[6].Troops.Should().Be(8);
        game.State.CurrentPlayer.Should().Be(1);
    }

    [Fact]
    public void Fortify_EnemyBreaksPath_RejectsMove()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Phase = Phase.Fortify;
        game.State.Territories[6].Owner = 0;
        game.State.Territories[0].Troops = 8;
        Action move = () => game.Apply(0, new() { Kind = CommandKind.Fortify, From = 0, To = 6, Count = 7 });
        move.Should().Throw<RuleException>();
        game.State.Territories[0].Troops.Should().Be(8);
    }
}
