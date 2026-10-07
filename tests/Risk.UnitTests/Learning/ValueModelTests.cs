using AwesomeAssertions;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Xunit;
namespace Risk.UnitTests.Learning;

public sealed class ValueModelTests
{
    [Fact]
    public void Ultimate_EmbeddedModel_LoadsAndKeepsCorrectionsWithinItsLimit()
    {
        var model = ValueModel.Ultimate;
        model.Members.Should().NotBeEmpty().And.OnlyContain(m => m.Inputs == BoardFeatures.Count);
        var game = TestWorld.Game(2, new(), 52);
        var master = new MasterStrategy();
        var corrections = new List<double>();
        for (var actions = 0; game.State.Phase != Phase.Finished && actions < 200; actions++)
        {
            var observation = GameObservation.From(game);
            var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
            var board = PlannerBoard.From(observation);
            if (observation.Phase == Phase.Attack) corrections.Add(model.Correction(evaluator, board, evaluator.Evaluate(board)));
            game.Apply(observation.Player, master.Choose(observation));
        }
        corrections.Should().NotBeEmpty().And.OnlyContain(c => double.IsFinite(c) && Math.Abs(c) <= model.Limit);
        corrections.Should().Contain(c => c != 0);
    }

    [Fact]
    public void Correction_ThreePlayerGame_LeavesTheHandScoreUnchanged()
    {
        var game = TestWorld.Game(3, new(), 53);
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
        var board = PlannerBoard.From(observation);
        ValueModel.Ultimate.Correction(evaluator, board, evaluator.Evaluate(board)).Should().Be(0);
    }

    [Fact]
    public void Correction_ObservationWithoutPlayerList_LeavesTheHandScoreUnchanged()
    {
        var game = TestWorld.Game(2, new(), 54);
        var observation = GameObservation.From(game) with { Players = null };
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
        var board = PlannerBoard.From(observation);
        ValueModel.Ultimate.Correction(evaluator, board, 0).Should().Be(0);
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesEveryNetworkOutput()
    {
        var model = new ValueModel { Scale = .5, Members = [ValueNetwork.Create(BoardFeatures.Count, 8, new Random(4))] };
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(model.Serialize()));
        var copy = ValueModel.Load(stream);
        var x = Enumerable.Range(0, BoardFeatures.Count).Select(i => (float)(i % 7) / 7).ToArray();
        copy.Scale.Should().Be(.5);
        copy.Members[0].Term(x, new float[8]).Should().Be(model.Members[0].Term(x, new float[8]));
    }

    [Fact]
    public void Load_MismatchedFeatureSet_IsRejected()
    {
        var model = new ValueModel { Features = "other", Members = [ValueNetwork.Create(BoardFeatures.Count, 2, new Random(1))] };
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(model.Serialize()));
        var load = () => ValueModel.Load(stream);
        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Extract_DuelPosition_EncodesOwnershipThreatAndScore()
    {
        var game = TestWorld.Game(2, new(), 55);
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
        var board = PlannerBoard.From(observation);
        var x = new float[BoardFeatures.Count];
        BoardFeatures.Extract(evaluator, board, 25, x);
        var owned = Enumerable.Range(0, 42).Count(id => board.Owners[id] == observation.Player);
        Enumerable.Range(0, 42).Sum(id => x[id * 14]).Should().Be(owned);
        Enumerable.Range(0, 42).Sum(id => x[id * 14 + 7]).Should().Be(42 - owned);
        x[BoardFeatures.ScoreIndex].Should().Be(.5f);
        // Each territory's threat is the best outright capture chance of an adjacent enemy stack.
        var target = Enumerable.Range(0, 42).First(id => board.Owners[id] == observation.Player);
        var expected = observation.Map.Territories[target].Neighbors.Where(n => board.Owners[n] != observation.Player && board.Troops[n] > 1)
            .Select(n => BattleOdds.Estimate(board.Troops[n] - 1, board.Troops[target]).WinChance).DefaultIfEmpty(0).Max();
        x[target * 14 + 6].Should().BeApproximately((float)expected, 1e-6f);
    }
}
