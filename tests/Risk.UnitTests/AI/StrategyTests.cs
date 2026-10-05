using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class StrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Automatic)]
    [InlineData(6, CardMode.Fixed, SetupMode.Manual)]
    public void Choose_FullMixedBotMatch_ProducesOnlyLegalMovesAndReachesWinner(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 17);
        var strategies = Enumerable.Range(0, players).Select(i => StrategyCatalog.Create((BotDifficulty)(i % 3), i + 40)).ToArray();
        var actions = 0;
        while (game.State.Phase != Phase.Finished && actions++ < 15000)
        {
            var current = game.State.CurrentPlayer;
            var observation = GameObservation.From(game);
            var action = strategies[current].Choose(observation);
            game.Apply(current, action);
            game.State.Deck.Concat(game.State.Players.SelectMany(p => p.Cards)).Should().HaveCount(44).And.OnlyHaveUniqueItems();
            game.State.Territories.Should().OnlyContain(t => t.Troops >= 0);
            if (game.State.Phase != Phase.Occupy && game.State.Phase != Phase.Claim)
                game.State.Territories.Should().OnlyContain(t => t.Troops > 0);
        }
        game.State.Phase.Should().Be(Phase.Finished);
        game.State.Winner.Should().BeInRange(0, players - 1);
    }

    [Fact]
    public void From_Observation_CopiesBoardAndIncludesOnlyCurrentPlayersHand()
    {
        var game = TestWorld.Game();
        game.State.Players[0].Cards.Add(1);
        game.State.Players[1].Cards.Add(2);
        var observation = GameObservation.From(game);
        observation.Cards.Should().Equal(1);
        var oldTroops = observation.Territories[0].Troops;
        game.State.Territories[0].Troops += 10;
        observation.Territories[0].Troops.Should().Be(oldTroops);
    }
}
