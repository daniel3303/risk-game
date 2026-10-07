using AwesomeAssertions;
using Risk.Arena;
using Risk.Arena.Models;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class MasterStrategyTests
{
    [Theory]
    [InlineData(2, CardMode.Fixed, SetupMode.Automatic)]
    [InlineData(3, CardMode.Progressive, SetupMode.Automatic)]
    [InlineData(6, CardMode.Fixed, SetupMode.Manual)]
    public void Choose_FullMasterMatch_AppliesLegalMovesAndPreservesCards(int players, CardMode cards, SetupMode setup)
    {
        var game = TestWorld.Game(players, new(cards, setup), 33);
        var strategies = Enumerable.Range(0, players).Select(id => StrategyCatalog.Create(id == 0 ? BotDifficulty.Master : BotDifficulty.Expert, id + 61)).ToArray();
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
    public void Run_DefaultTuning_ReproducesThePublishedExpertMatches()
    {
        var options = new ArenaOptions(Seeds: 2, FirstSeed: 3000, Candidate: BotDifficulty.Expert, Opponent: BotDifficulty.Hard);
        var report = ArenaRunner.Run(TestWorld.Map(), options, TestContext.Current.CancellationToken);
        // Recorded in docs/ai-results.json; tuning plumbing must not change Expert's moves.
        report.Matches.Should().Equal(new MatchResult(3000, 0, 0, true, 6, 148), new MatchResult(3000, 1, 1, true, 7, 168),
            new MatchResult(3001, 0, 0, true, 5, 131), new MatchResult(3001, 1, 1, true, 8, 212));
    }

    [Fact]
    public void Choose_ExpertGamePositions_MasterDeviatesFromExpert()
    {
        var game = TestWorld.Game(2, new(), 41);
        var expert = new ExpertStrategy();
        var master = new MasterStrategy();
        var differences = 0;
        while (game.State.Phase != Phase.Finished)
        {
            var observation = GameObservation.From(game);
            var command = expert.Choose(observation);
            var alternative = master.Choose(observation);
            // Card arrays compare by reference, so compare the move itself.
            if ((alternative.Kind, alternative.From, alternative.To, alternative.Count) != (command.Kind, command.From, command.To, command.Count)) differences++;
            game.Apply(observation.Player, command);
        }
        differences.Should().BePositive();
    }

    [Fact]
    public void Create_Master_ReturnsTheDeepPlanner()
    {
        StrategyCatalog.Create(BotDifficulty.Master, 1).Id.Should().Be("master-deep-planner");
        StrategyCatalog.Create(BotDifficulty.Expert, 1).Id.Should().Be("expert-turn-planner");
    }

    [Fact]
    public void Master_TuningPreset_SearchesWiderAndDeeperThanExpert()
    {
        var master = ExpertTuning.Master;
        var expert = ExpertTuning.Default;
        master.BeamWidth.Should().BeGreaterThan(expert.BeamWidth);
        master.Branches.Should().BeGreaterThan(expert.Branches);
        master.SearchBudget.Should().BeGreaterThan(expert.SearchBudget);
    }

    [Fact]
    public void Evaluate_DuelEnemyIncomeValue_ScalesThePenaltyForRivalContinentIncome()
    {
        var game = TestWorld.Game(2);
        foreach (var territory in game.State.Territories) { territory.Owner = 0; territory.Troops = 2; }
        game.State.CurrentPlayer = 0;
        game.State.Phase = Phase.Attack;
        var observation = GameObservation.From(game);
        var australia = new[] { 38, 39, 40, 41 };
        var rivalContinent = PlannerBoard.From(observation);
        foreach (var id in australia) rivalContinent.Owners[id] = 1;
        var rivalScattered = PlannerBoard.From(observation);
        foreach (var id in new[] { 0, 10, 20, 30 }) rivalScattered.Owners[id] = 1;

        double Penalty(ExpertTuning tuning)
        {
            var evaluator = new PositionEvaluator(observation, tuning);
            return evaluator.Evaluate(rivalScattered) - evaluator.Evaluate(rivalContinent);
        }

        var stronger = ExpertTuning.Default with { DuelEnemyIncomeValue = 8 };
        Penalty(stronger).Should().BeGreaterThan(Penalty(ExpertTuning.Default));
    }
}
