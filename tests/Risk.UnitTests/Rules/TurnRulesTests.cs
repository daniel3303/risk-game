using AwesomeAssertions;
using Risk.Sim;
using Risk.Sim.Models;
using Risk.Sim.Rules;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class TurnRulesTests
{
    [Fact]
    public void Reinforcements_FullAustralia_AddsContinentBonusAndMinimum()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        foreach (var territory in game.State.Territories.Where(t => game.Map.Territories[t.Id].Continent == "australia")) territory.Owner = 0;
        TurnRules.Reinforcements(game.Map, game.State, 0).Should().Be(5);
    }

    [Fact]
    public void Apply_PartialDraft_RemainsDraftUntilEveryTroopPlaced()
    {
        var game = TestWorld.Game();
        var territory = game.State.Territories.First(t => t.Owner == 0);
        var available = game.State.Reinforcements;
        game.Apply(0, new() { Kind = CommandKind.Place, To = territory.Id, Count = 1 });
        game.State.Phase.Should().Be(Phase.Draft);
        game.Apply(0, new() { Kind = CommandKind.Place, To = territory.Id, Count = available - 1 });
        game.State.Phase.Should().Be(Phase.Attack);
    }

    [Fact]
    public void Apply_WrongPlayerOrInvalidCount_LeavesBoardUnchanged()
    {
        var game = TestWorld.Game();
        var territory = game.State.Territories.First(t => t.Owner == 0);
        var before = territory.Troops;
        Action wrongPlayer = () => game.Apply(1, new() { Kind = CommandKind.Place, To = territory.Id, Count = 1 });
        Action tooMany = () => game.Apply(0, new() { Kind = CommandKind.Place, To = territory.Id, Count = 999 });
        wrongPlayer.Should().Throw<RuleException>();
        tooMany.Should().Throw<RuleException>();
        territory.Troops.Should().Be(before);
    }

    [Fact]
    public void End_ConqueredMultipleTerritories_DrawsOnlyOneCardAndSkipsEliminated()
    {
        var game = TestWorld.Game();
        game.State.Phase = Phase.Fortify;
        game.State.ConqueredThisTurn = true;
        game.State.Players[1].Eliminated = true;
        game.Apply(0, new() { Kind = CommandKind.EndTurn });
        game.State.Players[0].Cards.Should().ContainSingle();
        game.State.CurrentPlayer.Should().Be(2);
        game.State.Phase.Should().Be(Phase.Draft);
    }

    [Fact]
    public void End_NoConquest_DoesNotDrawCard()
    {
        var game = TestWorld.Game();
        game.State.Phase = Phase.Fortify;
        game.Apply(0, new() { Kind = CommandKind.EndTurn });
        game.State.Players[0].Cards.Should().BeEmpty();
    }
}
