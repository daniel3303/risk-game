using AwesomeAssertions;
using Risk.Sim.AI;
using Risk.Sim.AI.Probes;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI.Probes;

public sealed class TurtleStrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Manual)]
    public void Choose_FullMatch_AppliesLegalMovesAndPreservesCards(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 91);
        var strategies = Enumerable.Range(0, players).Select(id => PolicyCatalog.Create(id == 0 ? "turtle" : "master", id + 81)).ToArray();
        var actions = 0;
        while (game.State.Phase != Phase.Finished && actions++ < 15000)
        {
            var player = game.State.CurrentPlayer;
            game.Apply(player, strategies[player].Choose(GameObservation.From(game)));
            game.State.Deck.Concat(game.State.Players.SelectMany(p => p.Cards)).Should().HaveCount(44).And.OnlyHaveUniqueItems();
        }
        game.State.Phase.Should().Be(Phase.Finished);
    }

    [Fact]
    public void Choose_AfterItsCardCapture_StopsAttackingUntilItCanStrike()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        // One owned stack beside weak enemies that cannot be eliminated; the turn's card is already earned.
        game.State.Territories[0].Troops = 6;
        game.State.Territories[2].Owner = 1;
        game.State.ConqueredThisTurn = true;
        var turtle = new TurtleStrategy();
        turtle.Choose(GameObservation.From(game)).Kind.Should().Be(CommandKind.EndAttack);
        game.State.ConqueredThisTurn = false;
        var farm = turtle.Choose(GameObservation.From(game));
        farm.Kind.Should().Be(CommandKind.Attack);
        farm.From.Should().Be(0);
    }

    [Fact]
    public void Choose_WithAnOverwhelmingArmy_HandsTheTurnToExpertsPlanner()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        // No rival can be eliminated and the turn's card is earned, so a turtle that is not campaigning ends its attacks.
        game.State.Territories[2].Owner = 1;
        game.State.ConqueredThisTurn = true;
        game.State.Territories[0].Troops = 50;
        var turtle = new TurtleStrategy();
        turtle.Choose(GameObservation.From(game)).Kind.Should().Be(CommandKind.EndAttack);
        // Sixty armies against the strongest rival's thirty-nine: half again as many, so the probe campaigns like Expert.
        game.State.Territories[0].Troops = 60;
        var observation = GameObservation.From(game);
        var command = turtle.Choose(observation);
        var expert = new ExpertStrategy().Choose(observation);
        command.Kind.Should().Be(CommandKind.Attack);
        (command.Kind, command.From, command.To, command.Count).Should().Be((expert.Kind, expert.From, expert.To, expert.Count));
    }
}
