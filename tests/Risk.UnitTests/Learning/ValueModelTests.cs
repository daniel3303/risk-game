using System.Text;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
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
        model.Members.Should().NotBeEmpty().And.OnlyContain(m => m.Inputs == BoardEncoding.Duel.Count);
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
        var model = new ValueModel { Scale = .5, Members = [ValueNetwork.Create(BoardEncoding.Duel.Count, 8, new Random(4))] };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(model.Serialize()));
        var copy = ValueModel.Load(stream);
        var x = Enumerable.Range(0, BoardEncoding.Duel.Count).Select(i => (float)(i % 7) / 7).ToArray();
        copy.Scale.Should().Be(.5);
        copy.Members[0].Term(x, new float[8]).Should().Be(model.Members[0].Term(x, new float[8]));
    }

    [Theory]
    [InlineData("features")]
    [InlineData("schema")]
    [InlineData("inputs")]
    [InlineData("parameters")]
    [InlineData("missing parameters")]
    [InlineData("zero calibration")]
    [InlineData("not a number")]
    [InlineData("negative limit")]
    [InlineData("no members")]
    [InlineData("malformed")]
    public void Load_InvalidModel_IsRejected(string defect)
    {
        var network = ValueNetwork.Create(BoardEncoding.Duel.Count, 2, new Random(1));
        var model = JObject.Parse(new ValueModel { Members = [network] }.Serialize());
        var member = (JObject)model["Members"][0];
        switch (defect)
        {
            case "features": model["Features"] = "other"; break;
            case "schema": model["Schema"] = 2; break;
            case "inputs": member["Inputs"] = 620; break;
            case "parameters": member["Parameters"] = Convert.ToBase64String(new byte[12]); break;
            case "missing parameters": member.Remove("Parameters"); break;
            case "zero calibration": member["A"] = 0; break;
            case "not a number": member["B"] = double.NaN; break;
            case "negative limit": model["Limit"] = -1; break;
            case "no members": model["Members"] = null; break;
        }
        var json = defect == "malformed" ? "{ \"Schema\": 1, " : model.ToString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var load = () => ValueModel.Load(stream);
        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_ParametersBeforeSizes_StillSplitsTheWeights()
    {
        var network = ValueNetwork.Create(BoardEncoding.Duel.Count, 3, new Random(2));
        var model = JObject.Parse(new ValueModel { Members = [network] }.Serialize());
        var member = (JObject)model["Members"][0];
        var reordered = new JObject { ["Parameters"] = member["Parameters"], ["Inputs"] = member["Inputs"], ["Hidden"] = member["Hidden"], ["A"] = member["A"], ["B"] = member["B"] };
        model["Members"] = new JArray(reordered);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(model.ToString()));
        var copy = ValueModel.Load(stream).Members[0];
        copy.W.Should().Equal(network.W);
        copy.V.Should().Equal(network.V);
    }

    [Fact]
    public void Term_VectorisedSum_MatchesAPlainLoop()
    {
        var network = ValueNetwork.Create(BoardEncoding.Duel.Count, 64, new Random(6));
        for (var j = 0; j < 64; j++) network.C[j] = (j % 5 - 2) * .01f;
        var random = new Random(7);
        var x = Enumerable.Range(0, BoardEncoding.Duel.Count).Select(_ => random.NextDouble() < .3 ? (float)random.NextDouble() : 0f).ToArray();
        var expected = 0.0;
        for (var j = 0; j < 64; j++)
        {
            var unit = network.C[j];
            for (var i = 0; i < BoardEncoding.Duel.Count; i++) if (x[i] != 0) unit += network.W[i * 64 + j] * x[i];
            expected += (double)Math.Max(0, unit) * network.V[j];
        }
        network.Term(x, new float[64]).Should().Be(expected);
    }

    [Fact]
    public void Extract_DuelPosition_EncodesOwnershipThreatAndScore()
    {
        var game = TestWorld.Game(2, new(), 55);
        var observation = GameObservation.From(game);
        var evaluator = new PositionEvaluator(observation, ExpertTuning.Master);
        var board = PlannerBoard.From(observation);
        var x = new float[BoardEncoding.Duel.Count];
        BoardEncoding.Duel.Extract(evaluator, board, 25, x);
        var owned = Enumerable.Range(0, 42).Count(id => board.Owners[id] == observation.Player);
        Enumerable.Range(0, 42).Sum(id => x[id * 14]).Should().Be(owned);
        Enumerable.Range(0, 42).Sum(id => x[id * 14 + 7]).Should().Be(42 - owned);
        x[BoardEncoding.Duel.ScoreIndex].Should().Be(.5f);
        // Each territory's threat is the best outright capture chance of an adjacent enemy stack.
        var target = Enumerable.Range(0, 42).First(id => board.Owners[id] == observation.Player);
        var expected = observation.Map.Territories[target].Neighbors.Where(n => board.Owners[n] != observation.Player && board.Troops[n] > 1)
            .Select(n => BattleOdds.Estimate(board.Troops[n] - 1, board.Troops[target]).WinChance).DefaultIfEmpty(0).Max();
        x[target * 14 + 6].Should().BeApproximately((float)expected, 1e-6f);
    }
}
