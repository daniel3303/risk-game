using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class GameSetupTests
{
    [Theory]
    [InlineData(2, 40)] [InlineData(3, 35)] [InlineData(4, 30)] [InlineData(5, 25)] [InlineData(6, 20)]
    public void Create_AutomaticSetup_DistributesAllTerritoriesAndCorrectArmies(int players, int armies)
    {
        var game = TestWorld.Game(players);
        game.State.Territories.Should().HaveCount(42).And.OnlyContain(t => t.Troops > 0 && t.Owner >= 0);
        foreach (var player in game.State.Players)
            game.State.Territories.Where(t => t.Owner == player.Id).Sum(t => t.Troops).Should().Be(armies);
        game.State.Deck.Should().HaveCount(44).And.OnlyHaveUniqueItems();
        game.State.Phase.Should().Be(Phase.Draft);
    }

    [Fact]
    public void Create_SameSeed_ReproducesOwnershipArmiesAndDeck()
    {
        var first = TestWorld.Game();
        var second = TestWorld.Game();
        first.State.Territories.Select(t => (t.Owner, t.Troops)).Should().Equal(second.State.Territories.Select(t => (t.Owner, t.Troops)));
        first.State.Deck.Should().Equal(second.State.Deck);
    }

    [Fact]
    public void Apply_ManualSetup_ClaimsAndPlacesInOrderBeforeDraft()
    {
        var game = TestWorld.Game(4, new(CardMode.Fixed, SetupMode.Manual));
        for (var id = 0; id < 42; id++) game.Apply(game.State.CurrentPlayer, new() { Kind = CommandKind.Claim, To = id });
        game.State.Phase.Should().Be(Phase.Setup);
        while (game.State.Phase == Phase.Setup)
        {
            var player = game.State.CurrentPlayer;
            var territory = game.State.Territories.First(t => t.Owner == player);
            game.Apply(player, new() { Kind = CommandKind.Place, To = territory.Id, Count = 1 });
        }
        game.State.Players.Should().OnlyContain(p => p.SetupTroops == 0);
        game.State.Phase.Should().Be(Phase.Draft);
        game.State.CurrentPlayer.Should().Be(0);
        game.State.Reinforcements.Should().BeGreaterThanOrEqualTo(3);
        game.State.Territories.Sum(t => t.Troops).Should().Be(120);
    }

    [Fact]
    public void Apply_ClaimOwnedTerritory_RejectsWithoutAdvancing()
    {
        var game = TestWorld.Game(3, new(CardMode.Fixed, SetupMode.Manual));
        game.Apply(0, new() { Kind = CommandKind.Claim, To = 0 });
        Action invalid = () => game.Apply(1, new() { Kind = CommandKind.Claim, To = 0 });
        invalid.Should().Throw<RuleException>();
        game.State.CurrentPlayer.Should().Be(1);
    }
}
