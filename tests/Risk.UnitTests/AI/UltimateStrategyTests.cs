using AwesomeAssertions;
using Risk.Arena;
using Risk.Arena.Models;
using Risk.Sim;
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

    [Fact]
    public void Evaluate_RivalSides_WeighTheHumanRivalWithoutChangingTheHandScore()
    {
        var game = TestWorld.Game();
        // Player 0 holds two territories; even territories go to the human (player 2), odd ones to the bot (player 1).
        foreach (var territory in game.State.Territories) { territory.Owner = territory.Id < 2 ? 0 : 2 - territory.Id % 2; territory.Troops = 1; }
        game.State.Phase = Phase.Attack;
        var plain = GameObservation.From(game);
        var flagged = GameObservation.From(game, [2]);
        flagged.Players.Select(p => p.Human).Should().Equal(false, false, true);
        var board = PlannerBoard.From(flagged);
        var sides = ExpertTuning.Master with { HumanRivalWeight = 3, BotRivalWeight = .5 };
        // Weights of one, or no human rival, leave the score exactly as before.
        new PositionEvaluator(flagged, ExpertTuning.Master).Evaluate(board).Should().Be(new PositionEvaluator(plain, ExpertTuning.Master).Evaluate(board));
        new PositionEvaluator(plain, sides).Evaluate(board).Should().Be(new PositionEvaluator(plain, ExpertTuning.Master).Evaluate(board));
        var evaluator = new PositionEvaluator(flagged, sides);
        evaluator.HandScore(board).Should().Be(new PositionEvaluator(plain, ExpertTuning.Master).HandScore(board));
        var human = board.Copy();
        human.Troops[2] += 10;
        var bot = board.Copy();
        bot.Troops[3] += 10;
        // Ten more armies cost 0.3 each, three times for the human and half for the bot: 10 * 0.3 * (3 - 0.5).
        (evaluator.Evaluate(bot) - evaluator.Evaluate(human)).Should().BeApproximately(7.5, 1e-9);
        // People alone among the rivals leave no bot to side with, so the weights stay off.
        new PositionEvaluator(GameObservation.From(game, [1, 2]), sides).Evaluate(human).Should().Be(new PositionEvaluator(plain, ExpertTuning.Master).Evaluate(human));
        // Two players left: the weights stay off in the duel.
        game.State.Players[1].Eliminated = true;
        new PositionEvaluator(GameObservation.From(game, [2]), sides).Evaluate(human).Should().Be(new PositionEvaluator(GameObservation.From(game), ExpertTuning.Master).Evaluate(human));
    }

    [Fact]
    public void Evaluate_BotEndgame_SwapsTheThreatExponentOnlyOnceThePeopleAreOut()
    {
        var game = TestWorld.Game(4);
        // Player 0 holds two territories, bots 1 and 2 split the rest, and the human (player 3) is already out.
        foreach (var territory in game.State.Territories) { territory.Owner = territory.Id < 2 ? 0 : 1 + territory.Id % 2; territory.Troops = 1; }
        game.State.Players[3].Eliminated = true;
        game.State.Phase = Phase.Attack;
        var plain = GameObservation.From(game);
        var flagged = GameObservation.From(game, [3]);
        var board = PlannerBoard.From(flagged);
        board.Troops[2] = 31;
        var threat = ExpertTuning.Master with { ThreatWeighting = 1.5 };
        var flat = threat with { BotEndgameThreatWeighting = 0 };
        var evaluator = new PositionEvaluator(flagged, flat);
        // The learned correction keeps reading the threat-weighted score; the bots-only exponent weighs the rivals afterwards.
        evaluator.HandScore(board).Should().Be(new PositionEvaluator(flagged, threat).HandScore(board));
        evaluator.Evaluate(board).Should().BeApproximately(new PositionEvaluator(flagged, ExpertTuning.Master).Evaluate(board), 1e-9);
        evaluator.Evaluate(board).Should().NotBe(new PositionEvaluator(flagged, threat).Evaluate(board));
        // No person at the table: the ordinary exponent stays.
        new PositionEvaluator(plain, flat).Evaluate(board).Should().Be(new PositionEvaluator(plain, threat).Evaluate(board));
        // While the human is still in, the exponent stays off.
        game.State.Players[3].Eliminated = false;
        game.State.Territories[4].Owner = 3;
        var alive = GameObservation.From(game, [3]);
        var held = PlannerBoard.From(alive);
        new PositionEvaluator(alive, flat).Evaluate(held).Should().Be(new PositionEvaluator(alive, threat).Evaluate(held));
    }

    [Fact]
    public void Play_FourPlayerTurtleSeed_BotsFinishOnceTheProbeIsOut()
    {
        // Seed 1280004 of the bots-only endgame screen, the probe in the first seat, seated as the Arena seats it. The probe falls
        // at round 22; with the shipped exponent the three Ultimates finish at round 49, without it they still fight at round 200.
        const int seed = 1280004;
        string[] policies = ["turtle", "ultimate", "ultimate", "ultimate"];
        var game = new Game(TestWorld.Map(), policies.Select((p, seat) => $"{p} {seat + 1}").ToArray(), new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(seed));
        var strategies = policies.Select((policy, seat) => PolicyCatalog.Create(policy, unchecked(seed * 1000003 + seat * 7919))).ToArray();
        var humans = ArenaRunner.HumanSeats(policies);
        var actions = 0;
        while (game.State.Phase != Phase.Finished && game.State.Round <= 200)
        {
            game.Apply(game.State.CurrentPlayer, strategies[game.State.CurrentPlayer].Choose(GameObservation.From(game, humans)));
            actions++;
        }
        (game.State.Phase, game.State.Winner, game.State.Round, actions).Should().Be((Phase.Finished, 3, 49, 1081));
    }

    [Theory]
    [InlineData(1272000, 0, 21, 685, 1, 16, 575, 2, 17, 590)]
    [InlineData(1272001, 0, 14, 442, 2, 17, 539, 2, 10, 352)]
    public void Run_ThreePlayerSeeds_ReproduceThePublishedUltimateMatches(int seed, int winner0, int rounds0, int actions0,
        int winner1, int rounds1, int actions1, int winner2, int rounds2, int actions2)
    {
        var options = new ArenaOptions(Seeds: 1, FirstSeed: seed, Players: 3, Candidate: "ultimate", Opponent: "master");
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        // The first matches of the published three-player evaluation; they change if the multiplayer terms or model change. No
        // person sits at this table, so the rival weights are pinned by the turtle replays below instead.
        report.Matches.Should().Equal(new MatchResult(seed, 0, winner0, true, rounds0, actions0),
            new MatchResult(seed, 1, winner1, true, rounds1, actions1), new MatchResult(seed, 2, winner2, true, rounds2, actions2));
    }

    [Theory]
    [InlineData(1270000, 1, 9, 194, 2, 16, 381, 0, 9, 214)]
    [InlineData(1270001, 1, 21, 499, 2, 13, 346, 0, 20, 530)]
    public void Run_TurtleSeeds_ReproduceThePublishedProbeMatches(int seed, int winner0, int rounds0, int actions0,
        int winner1, int rounds1, int actions1, int winner2, int rounds2, int actions2)
    {
        var options = new ArenaOptions(Seeds: 1, FirstSeed: seed, Players: 3, Candidate: "turtle", Opponent: "ultimate");
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        // The first matches of the published probe evaluation, the turtle seated as the human; they change if the rival weights change.
        report.Matches.Should().Equal(new MatchResult(seed, 0, winner0, true, rounds0, actions0),
            new MatchResult(seed, 1, winner1, true, rounds1, actions1), new MatchResult(seed, 2, winner2, true, rounds2, actions2));
    }

    [Fact]
    public void Ultimate_RivalWeights_SideWithBotsAgainstPeopleWhileMasterStaysNeutral()
    {
        (ExpertTuning.Ultimate.HumanRivalWeight, ExpertTuning.Ultimate.BotRivalWeight).Should().Be((3, .5));
        (ExpertTuning.Master.HumanRivalWeight, ExpertTuning.Master.BotRivalWeight).Should().Be((1, 1));
        (ExpertTuning.Default.HumanRivalWeight, ExpertTuning.Default.BotRivalWeight).Should().Be((1, 1));
        ExpertTuning.Ultimate.BotEndgameThreatWeighting.Should().Be(-1);
        ExpertTuning.Master.BotEndgameThreatWeighting.Should().BeNull();
    }

    [Fact]
    public void Create_Ultimate_ReturnsTheLearnedPlanner()
    {
        StrategyCatalog.Create(BotDifficulty.Ultimate, 1).Id.Should().Be("ultimate-learned-planner");
    }
}
