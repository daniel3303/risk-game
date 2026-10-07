using AwesomeAssertions;
using Risk.Sim.AI;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class MasterStrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Automatic)]
    [InlineData(6, CardMode.Fixed, SetupMode.Manual)]
    public void Choose_FullMasterMatch_AppliesLegalMovesAndPreservesCards(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 33);
        var strategies = Enumerable.Range(0, players).Select(id => StrategyCatalog.Create(id == 0 ? BotDifficulty.Master : BotDifficulty.Expert, id + 61)).ToArray();
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
    public void Choose_ExpertGamePositions_MasterDeviatesFromExpert()
    {
        var game = TestWorld.Game(2, new(), 41);
        var expert = new ExpertStrategy();
        var master = new MasterStrategy();
        var differences = 0;
        while (game.State.Phase != Phase.Finished)
        {
            var observation = GameObservation.From(game);
            var command = expert.Choose(observation);
            var alternative = master.Choose(observation);
            // Card arrays compare by reference, so compare the move itself.
            if ((alternative.Kind, alternative.From, alternative.To, alternative.Count) != (command.Kind, command.From, command.To, command.Count)) differences++;
            game.Apply(observation.Player, command);
        }
        differences.Should().BePositive();
    }

    [Fact]
    public void Create_Master_ReturnsTheDeepPlanner()
    {
        StrategyCatalog.Create(BotDifficulty.Master, 1).Id.Should().Be("master-deep-planner");
        StrategyCatalog.Create(BotDifficulty.Expert, 1).Id.Should().Be("expert-turn-planner");
    }
}
