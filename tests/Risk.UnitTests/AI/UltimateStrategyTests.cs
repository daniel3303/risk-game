using AwesomeAssertions;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class UltimateStrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Automatic)]
    [InlineData(6, CardMode.Fixed, SetupMode.Manual)]
    public void Choose_FullUltimateMatch_AppliesLegalMovesAndPreservesCards(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 35);
        var strategies = Enumerable.Range(0, players).Select(id => StrategyCatalog.Create(id == 0 ? BotDifficulty.Ultimate : BotDifficulty.Master, id + 71)).ToArray();
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
    public void Choose_ZeroCorrection_ReproducesMaster()
    {
        var zero = new ValueModel { Members = [new ValueNetwork { Inputs = BoardFeatures.Count, Hidden = 4, A = 1, W = new float[BoardFeatures.Count * 4], C = new float[4], V = new float[4] }] };
        var game = TestWorld.Game(2, new(), 43);
        var master = new MasterStrategy();
        var corrected = new ExpertStrategy(ExpertTuning.Master with { Valuation = zero });
        while (game.State.Phase != Phase.Finished)
        {
            var observation = GameObservation.From(game);
            var command = master.Choose(observation);
            var alternative = corrected.Choose(observation);
            // Card arrays compare by reference, so compare the move itself.
            (alternative.Kind, alternative.From, alternative.To, alternative.Count).Should().Be((command.Kind, command.From, command.To, command.Count));
            game.Apply(observation.Player, command);
        }
    }

    [Fact]
    public void Choose_ExpertGamePositions_UltimateDeviatesFromMaster()
    {
        var game = TestWorld.Game(2, new(), 47);
        var master = new MasterStrategy();
        var ultimate = new UltimateStrategy();
        var differences = 0;
        while (game.State.Phase != Phase.Finished)
        {
            var observation = GameObservation.From(game);
            var command = master.Choose(observation);
            var alternative = ultimate.Choose(observation);
            if ((alternative.Kind, alternative.From, alternative.To, alternative.Count) != (command.Kind, command.From, command.To, command.Count)) differences++;
            game.Apply(observation.Player, command);
        }
        differences.Should().BePositive();
    }

    [Fact]
    public void Create_Ultimate_ReturnsTheLearnedPlanner()
    {
        StrategyCatalog.Create(BotDifficulty.Ultimate, 1).Id.Should().Be("ultimate-learned-planner");
    }
}
