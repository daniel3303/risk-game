using AwesomeAssertions;
using Risk.Arena;
using Risk.Arena.Models;
using Risk.Sim.AI;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class ArenaTests
{
    [Fact]
    public void Run_IdenticalPolicies_RotatesAllStartingSeatsWithoutFavoringTheCandidate()
    {
        var options = new ArenaOptions(Seeds: 2, Players: 2, Candidate: BotDifficulty.Easy, Opponent: BotDifficulty.Easy);
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        report.Games.Should().Be(4);
        report.Wins.Should().Be(2);
        report.Losses.Should().Be(2);
        report.Unfinished.Should().Be(0);
        report.Matches.GroupBy(m => m.Seed).Should().OnlyContain(group => group.Select(m => m.CandidateSeat).Order().SequenceEqual(new[] { 0, 1 }));
    }

    [Fact]
    public void Run_Parallel_ReproducesSequentialMatches()
    {
        var sequential = ArenaRunner.Run(TestWorld.Map(), new ArenaOptions(Seeds: 3, Candidate: BotDifficulty.Master, Opponent: BotDifficulty.Hard), TestContext.Current.CancellationToken);
        var parallel = ArenaRunner.Run(TestWorld.Map(), new ArenaOptions(Seeds: 3, Candidate: BotDifficulty.Master, Opponent: BotDifficulty.Hard, Parallelism: 4), TestContext.Current.CancellationToken);
        parallel.Matches.Should().Equal(sequential.Matches);
        parallel.Wins.Should().Be(sequential.Wins);
    }

    [Fact]
    public void Run_TruncatedGames_ReportsNoInventedWinners()
    {
        var report = ArenaRunner.Run(TestWorld.Map(), new ArenaOptions(Seeds: 1, MaxActions: 1), TestContext.Current.CancellationToken);
        report.Unfinished.Should().Be(2);
        report.Wins.Should().Be(0);
        report.Losses.Should().Be(0);
        report.WinRate.Should().Be(0);
        report.Matches.Should().OnlyContain(m => m.Winner == -1 && m.Actions == 1);
    }

    [Fact]
    public void Parse_UnknownOrDuplicateOptions_RejectsAmbiguousConfigurations()
    {
        Action unknown = () => ArenaOptions.Parse(["--opponent", "legendary"]);
        unknown.Should().Throw<ArgumentException>();
        Action duplicate = () => ArenaOptions.Parse(["--seeds", "1", "--seeds", "2"]);
        duplicate.Should().Throw<ArgumentException>();
        Action unpaired = () => ArenaOptions.Parse(["--players"]);
        unpaired.Should().Throw<ArgumentException>();
        Action invalid = () => ArenaOptions.Parse(["--players", "7"]);
        invalid.Should().Throw<ArgumentException>();
        Action serial = () => ArenaOptions.Parse(["--parallel", "0"]);
        serial.Should().Throw<ArgumentException>();
    }
}
