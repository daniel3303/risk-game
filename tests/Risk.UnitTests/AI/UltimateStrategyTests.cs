using AwesomeAssertions;
using Risk.Arena;
using Risk.Arena.Models;
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
        var zero = new ValueModel { Members = [new ValueNetwork { Inputs = BoardEncoding.Duel.Count, Hidden = 4, A = 1, W = new float[BoardEncoding.Duel.Count * 4], C = new float[4], V = new float[4] }] };
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
    public void Choose_ThreePlayerPositions_UltimateDeviatesFromMaster()
    {
        var game = TestWorld.Game(3, new(), 53);
        var master = new MasterStrategy();
        var ultimate = new UltimateStrategy();
        var differences = 0;
        var actions = 0;
        while (game.State.Phase != Phase.Finished && actions++ < 15000)
        {
            var observation = GameObservation.From(game);
            var command = master.Choose(observation);
            var alternative = ultimate.Choose(observation);
            if ((alternative.Kind, alternative.From, alternative.To, alternative.Count) != (command.Kind, command.From, command.To, command.Count)) differences++;
            game.Apply(observation.Player, command);
        }
        // Before frontier defence and threat weighting, Ultimate was Master whenever more than two players remained.
        differences.Should().BePositive();
    }

    [Fact]
    public void Evaluate_FrontierRisk_PenalisesAnExposedSingleArmyMoreThanAGarrison()
    {
        var game = TestWorld.Game(4);
        TestWorld.Board(game);
        // Territory 0 is player 0's only territory; its neighbour 3 holds player 1's stack of four with a small income behind it.
        game.State.Territories[3].Owner = 1;
        game.State.Territories[3].Troops = 4;
        var observation = GameObservation.From(game);
        var plain = new PositionEvaluator(observation, ExpertTuning.Master);
        var defensive = new PositionEvaluator(observation, ExpertTuning.Master with { FrontierRiskValue = 1.5 });
        defensive.FrontierWeight.Should().Be(1.5);
        var exposed = PlannerBoard.From(observation);
        var garrisoned = exposed.Copy();
        garrisoned.Troops[0] = 12;
        var exposedRisk = plain.Evaluate(exposed) - defensive.Evaluate(exposed);
        var garrisonedRisk = plain.Evaluate(garrisoned) - defensive.Evaluate(garrisoned);
        exposedRisk.Should().BeGreaterThan(5).And.BeGreaterThan(garrisonedRisk * 10);
        // Surrendered armies never attack, so they create no risk.
        game.State.Players[1].Eliminated = true;
        var passive = GameObservation.From(game);
        (new PositionEvaluator(passive, ExpertTuning.Master).Evaluate(exposed) - new PositionEvaluator(passive, ExpertTuning.Master with { FrontierRiskValue = 1.5 }).Evaluate(exposed)).Should().Be(0);
    }

    [Fact]
    public void Occupations_SafeGarrison_IsPlannedOnlyWhenFrontierRiskIsScored()
    {
        var game = TestWorld.Game();
        TestWorld.Board(game);
        game.State.Territories[0].Troops = 20;
        game.State.Territories[3].Troops = 6;
        var observation = GameObservation.From(game);
        var board = PlannerBoard.From(observation);
        // Plan the capture of territory 1 from territory 0, which stays adjacent to the rival stack on territory 3.
        board.Owners[1] = 0; board.Troops[1] = 19; board.Troops[0] = 1; board.Conquered = true;
        var master = new AttackPlanner(new PositionEvaluator(observation, ExpertTuning.Master)).Occupations(board, 0, 1, 3).ToArray();
        var ultimate = new AttackPlanner(new PositionEvaluator(observation, ExpertTuning.Ultimate)).Occupations(board, 0, 1, 3).ToArray();
        master.Should().HaveCount(1);
        ultimate.Should().HaveCount(2);
        ultimate[1].Troops[0].Should().BeGreaterThan(1);
        (ultimate[1].Troops[0] + ultimate[1].Troops[1]).Should().Be(20);
        // Two players left: the duel weight is zero, so Ultimate plans exactly Master's occupations.
        game.State.Players[1].Eliminated = true;
        new AttackPlanner(new PositionEvaluator(GameObservation.From(game), ExpertTuning.Ultimate)).Occupations(board, 0, 1, 3).Should().HaveCount(1);
    }

    [Theory]
    [InlineData(900000, 1, 8, 211, 0, 10, 283)]
    [InlineData(900001, 1, 10, 288, 1, 10, 276)]
    public void Run_DuelSeeds_ReproduceThePublishedUltimateMatches(int seed, int winner0, int rounds0, int actions0, int winner1, int rounds1, int actions1)
    {
        var options = new ArenaOptions(Seeds: 1, FirstSeed: seed, Players: 2, Candidate: "ultimate", Opponent: "master");
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        // Recorded in docs/ultimate-results.json before the multiplayer terms existed; duels must keep the evaluated model's exact decisions.
        report.Matches.Should().Equal(new MatchResult(seed, 0, winner0, true, rounds0, actions0), new MatchResult(seed, 1, winner1, true, rounds1, actions1));
    }

    [Fact]
    public void Evaluate_ThreatWeighting_LeavesEqualRivalsAloneAndWeighsTheStrongerRivalMore()
    {
        var game = TestWorld.Game();
        // Player 0 holds two territories; the rivals split the other forty evenly with one army each and no continent.
        foreach (var territory in game.State.Territories) { territory.Owner = territory.Id < 2 ? 0 : 2 - territory.Id % 2; territory.Troops = 1; }
        game.State.Phase = Phase.Attack;
        var observation = GameObservation.From(game);
        var board = PlannerBoard.From(observation);
        var master = new PositionEvaluator(observation, ExpertTuning.Master);
        var weighted = new PositionEvaluator(observation, ExpertTuning.Master with { ThreatWeighting = 1.5 });
        weighted.Evaluate(board).Should().Be(master.Evaluate(board));
        var lopsided = board.Copy();
        lopsided.Troops[2] = 31;
        (master.Evaluate(lopsided) - weighted.Evaluate(lopsided)).Should().BePositive();
    }

    [Theory]
    [InlineData(981000, 0, 17, 612, 0, 28, 944, 2, 14, 518)]
    [InlineData(981001, 1, 10, 349, 1, 26, 928, 1, 18, 637)]
    public void Run_ThreePlayerSeeds_ReproduceThePublishedUltimateMatches(int seed, int winner0, int rounds0, int actions0,
        int winner1, int rounds1, int actions1, int winner2, int rounds2, int actions2)
    {
        var options = new ArenaOptions(Seeds: 1, FirstSeed: seed, Players: 3, Candidate: "ultimate", Opponent: "master");
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        // The first matches of the published three-player evaluation; they change if the multiplayer terms, weights or model change.
        report.Matches.Should().Equal(new MatchResult(seed, 0, winner0, true, rounds0, actions0),
            new MatchResult(seed, 1, winner1, true, rounds1, actions1), new MatchResult(seed, 2, winner2, true, rounds2, actions2));
    }

    [Fact]
    public void Create_Ultimate_ReturnsTheLearnedPlanner()
    {
        StrategyCatalog.Create(BotDifficulty.Ultimate, 1).Id.Should().Be("ultimate-learned-planner");
    }
}
