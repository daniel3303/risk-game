using AwesomeAssertions;
using Newtonsoft.Json;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class ExpertStrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Automatic)]
    [InlineData(6, CardMode.Fixed, SetupMode.Manual)]
    public void Choose_FullExpertMatch_AppliesLegalMovesAndPreservesCards(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 27);
        var strategies = Enumerable.Range(0, players).Select(id => StrategyCatalog.Create(id == 0 ? BotDifficulty.Expert : (BotDifficulty)(id % 3), id + 51)).ToArray();
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
    public void Choose_Trade_SelectsTheBestFixedSetAndOwnedTerritoryBonus()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Phase = Phase.Draft;
        game.State.Reinforcements = 3;
        game.State.Players[0].Cards.AddRange([0, 3, 6, 1, 2]);
        var action = new ExpertStrategy().Choose(GameObservation.From(game));
        Sim.Rules.CardRules.FixedBonus(action.Cards).Should().Be(10);
        action.BonusTerritory.Should().Be(0);
        game.Apply(0, action);
        game.State.Reinforcements.Should().Be(13);
        game.State.Territories[0].Troops.Should().Be(3);
    }

    [Fact]
    public void Choose_LastEnemyTerritory_PrefersEliminationOverFarming()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 8;
        game.State.Players[1].Cards.AddRange([7, 8, 9, 10]);
        var action = new ExpertStrategy().Choose(GameObservation.From(game));
        action.Kind.Should().Be(CommandKind.Attack);
        action.To.Should().Be(1);
        game.Apply(0, action);
    }

    [Fact]
    public void Choose_LargeWinningArmy_DoesNotValueMaterialAboveVictory()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 50000;
        game.State.Players[2].Eliminated = true;
        var action = new ExpertStrategy().Choose(GameObservation.From(game));
        action.Kind.Should().Be(CommandKind.Attack);
        action.To.Should().Be(1);
    }

    [Fact]
    public void Choose_Observation_DoesNotMutateBoardOrRevealEnemyCardIdentities()
    {
        var game = TestWorld.Game();
        game.State.Players[1].Cards.AddRange([7, 8]);
        var observation = GameObservation.From(game);
        var before = JsonConvert.SerializeObject(game.State);
        var copied = JsonConvert.SerializeObject(observation);
        new ExpertStrategy().Choose(observation);
        JsonConvert.SerializeObject(game.State).Should().Be(before);
        JsonConvert.SerializeObject(observation).Should().Be(copied);
        observation.Players.Single(p => p.Id == 1).CardCount.Should().Be(2);
        observation.Players.Single(p => p.Id == 1).GetType().GetProperties().Select(p => p.Name).Should().Equal("Id", "Eliminated", "CardCount");
        observation.Cards.Should().BeEmpty();
    }

    [Fact]
    public void ForecastAttackers_PublicCardCounts_AnticipatesTradesWithoutReadingTheirSymbols()
    {
        var game = TestWorld.Game();
        var observation = GameObservation.From(game);
        var id = observation.Territories.First(t => t.Owner == 1).Id;
        var quiet = new PositionEvaluator(observation).ForecastAttackers(PlannerBoard.From(observation), id);
        var rich = observation with { Players = observation.Players.Select(p => p.Id == 1 ? p with { CardCount = 5 } : p).ToArray() };
        new PositionEvaluator(rich).ForecastAttackers(PlannerBoard.From(rich), id).Should().BeGreaterThan(quiet);
    }

    [Fact]
    public void Choose_Occupation_RespectsMandatoryMovementAndLeavesOneBehind()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Phase = Phase.Occupy;
        game.State.Territories[0].Troops = 10;
        game.State.Territories[1].Troops = 0;
        game.State.Capture = new(0, 1, 3, 9);
        var action = new ExpertStrategy().Choose(GameObservation.From(game));
        action.Count.Should().BeInRange(3, 9);
        game.Apply(0, action);
        game.State.Territories[0].Troops.Should().BeGreaterThanOrEqualTo(1);
    }
}
