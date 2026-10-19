using AwesomeAssertions;
using Risk.Arena;
using Risk.Arena.Models;
using Risk.Sim.AI;
using Risk.Sim.AI.Probes;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class PolicyCatalogTests
{
    [Fact]
    public void Names_ListEveryLobbyDifficultyAndEveryProbe()
    {
        PolicyCatalog.Names.Should().Contain(["easy", "normal", "hard", "expert", "master", "ultimate", "turtle"]);
        PolicyCatalog.IsKnown("Ultimate").Should().BeTrue();
        PolicyCatalog.IsKnown("legendary").Should().BeFalse();
        PolicyCatalog.IsKnown(null).Should().BeFalse();
    }

    [Fact]
    public void Create_ResolvesDifficultiesAndProbesByName()
    {
        PolicyCatalog.Create("master", 1).Id.Should().Be("master-deep-planner");
        PolicyCatalog.Create("Turtle", 1).Should().BeOfType<TurtleStrategy>().Which.Id.Should().Be("probe-turtle");
        PolicyCatalog.Names.Select(name => PolicyCatalog.Create(name, 1).Id).Should().OnlyHaveUniqueItems();
        Action unknown = () => PolicyCatalog.Create("5", 1);
        unknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_ProbeNames_AreAcceptedOnlyForTheArena()
    {
        ArenaOptions.Parse(["--candidate", "Turtle", "--opponent", "ultimate"]).Candidate.Should().Be("turtle");
        Enum.GetNames<BotDifficulty>().Should().NotContain(name => PolicyCatalog.Probes.Contains(name.ToLowerInvariant()));
    }

    [Fact]
    public void HumanSeats_ArenaPolicies_FlagsOnlyProbes()
    {
        ArenaRunner.HumanSeats(["ultimate", "turtle", "master", "Turtle"]).Should().Equal(1, 3);
        ArenaRunner.HumanSeats(["ultimate", "master"]).Should().BeEmpty();
    }

    [Fact]
    public void Create_SmgPreset_IsSeatableButNeverALobbyDifficulty()
    {
        PolicyCatalog.Create("SMG", 1).Should().BeOfType<SmgStrategy>().Which.Id.Should().Be("smg-learned-planner");
        Enum.GetNames<BotDifficulty>().Should().NotContain(name => PolicyCatalog.Presets.Contains(name.ToLowerInvariant()));
        ArenaRunner.HumanSeats(["smg", "master"]).Should().BeEmpty();
    }

    [Fact]
    public void Parse_Dice_DefaultsToTrueRandomAndAcceptsBalancedBlitz()
    {
        ArenaOptions.Parse([]).Dice.Should().Be(DiceMode.TrueRandom);
        ArenaOptions.Parse(["--dice", "BalancedBlitz"]).Dice.Should().Be(DiceMode.BalancedBlitz);
        Action unknown = () => ArenaOptions.Parse(["--dice", "loaded"]);
        unknown.Should().Throw<ArgumentException>();
    }
}
