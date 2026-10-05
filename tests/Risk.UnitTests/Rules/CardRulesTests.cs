using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Risk.Sim.Rules;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class CardRulesTests
{
    [Theory]
    [InlineData(0, 3, 6, 4)] [InlineData(1, 4, 7, 6)] [InlineData(2, 5, 8, 8)]
    [InlineData(0, 1, 2, 10)] [InlineData(0, 1, 42, 10)] [InlineData(2, 5, 42, 8)]
    [InlineData(0, 42, 43, 10)] [InlineData(0, 3, 1, 0)]
    public void FixedBonus_CardCombination_ReturnsOfficialValue(int a, int b, int c, int expected)
        => CardRules.FixedBonus([a, b, c]).Should().Be(expected);

    [Theory]
    [InlineData(0, 4)] [InlineData(1, 6)] [InlineData(4, 12)] [InlineData(5, 15)] [InlineData(6, 20)] [InlineData(7, 25)]
    public void ProgressiveBonus_GlobalTradeNumber_IncreasesAcrossAllPlayers(int trades, int expected)
        => CardRules.ProgressiveBonus(trades).Should().Be(expected);

    [Fact]
    public void Trade_OwnedCards_AddsOnlyOneTerritoryBonusAndRecyclesSet()
    {
        var game = TestWorld.Game();
        game.State.Players[0].Cards.AddRange([0, 3, 6]);
        foreach (var id in new[] { 0, 3, 6 }) { game.State.Territories[id].Owner = 0; game.State.Territories[id].Troops = 1; }
        var before = game.State.Reinforcements;
        game.Apply(0, new() { Kind = CommandKind.Trade, Cards = [0, 3, 6], BonusTerritory = 3 });
        game.State.Reinforcements.Should().Be(before + 4);
        game.State.Territories[3].Troops.Should().Be(3);
        game.State.Territories[0].Troops.Should().Be(1);
        game.State.Territories[6].Troops.Should().Be(1);
        game.State.Deck.TakeLast(3).Should().Equal(0, 3, 6);
    }

    [Fact]
    public void Trade_DuplicateCards_RejectsWithoutChangingHand()
    {
        var game = TestWorld.Game();
        game.State.Players[0].Cards.AddRange([0, 3, 6]);
        Action trade = () => game.Apply(0, new() { Kind = CommandKind.Trade, Cards = [0, 0, 0] });
        trade.Should().Throw<RuleException>();
        game.State.Players[0].Cards.Should().Equal(0, 3, 6);
    }

    [Fact]
    public void Place_FiveCards_RequiresTradeFirst()
    {
        var game = TestWorld.Game();
        game.State.Players[0].Cards.AddRange([0, 1, 2, 3, 4]);
        var territory = game.State.Territories.First(t => t.Owner == 0);
        Action place = () => game.Apply(0, new() { Kind = CommandKind.Place, To = territory.Id, Count = 1 });
        place.Should().Throw<RuleException>();
    }

    [Fact]
    public void FindSet_AnyFiveDistinctCards_AlwaysProvidesAValidTrade()
    {
        foreach (var hand in new int[][] { [0,3,1,4,2], [0,3,6,9,12], [0,1,4,7,42], [0,1,2,42,43] })
            CardRules.FixedBonus(CardRules.FindSet(hand)).Should().BeGreaterThan(0);
    }
}
