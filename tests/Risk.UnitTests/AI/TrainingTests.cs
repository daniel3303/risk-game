using AwesomeAssertions;
using Newtonsoft.Json;
using Risk.Sim.AI;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Risk.Training;
using Risk.Training.Models;
using Xunit;
namespace Risk.UnitTests.AI;

public sealed class TrainingTests
{
    [Fact]
    public void Deserialize_OmittedLimits_PreservesTheProtocolsDefaults()
    {
        var request = JsonConvert.DeserializeObject<TrainingRequest>("{\"operation\":\"reset\",\"seed\":4000,\"seat\":0,\"opponent\":\"expert\"}");
        request.MaxActions.Should().Be(5000);
        request.MaxRounds.Should().Be(100);
        new TrainingSession(TestWorld.Map()).Apply(request).Observation.Should().NotBeNull();
    }

    [Theory]
    [InlineData(51, 0, "expert")]
    [InlineData(52, 1, "hard")]
    [InlineData(53, 0, "normal")]
    public void Teacher_CompleteGames_ContainsEveryExpertDecisionAndAppliesIt(int seed, int seat, string opponent)
    {
        var session = new TrainingSession(TestWorld.Map());
        var frame = session.Apply(new("reset", seed, seat, opponent));
        while (!frame.Terminated && !frame.Truncated)
        {
            frame.Observation.State.Should().HaveCount(ObservationEncoder.StateSize).And.OnlyContain(value => float.IsFinite(value));
            frame.Observation.Candidates.Should().HaveCountLessThanOrEqualTo(ActionCatalog.Capacity);
            frame.Observation.Candidates.Should().OnlyContain(c => c.Length == ObservationEncoder.CandidateSize && c.All(float.IsFinite));
            var teacher = session.Apply(new("teacher"));
            teacher.Teacher.Should().BeInRange(0, frame.Observation.Candidates.Length - 1);
            frame = session.Apply(new("step", Action: teacher.Teacher));
        }
        frame.Terminated.Should().BeTrue();
        frame.Reward.Should().Be(frame.Winner == seat ? 1 : -1);
    }

    [Fact]
    public void Step_InvalidIndex_IsRejectedWithoutAdvancingTheGame()
    {
        var session = new TrainingSession(TestWorld.Map());
        var frame = session.Apply(new("reset"));
        var action = () => session.Apply(new("step", Action: ActionCatalog.Capacity));
        action.Should().Throw<ArgumentException>();
        var after = session.Apply(new("teacher"));
        JsonConvert.SerializeObject(after.Observation).Should().Be(JsonConvert.SerializeObject(frame.Observation));
        after.Actions.Should().Be(frame.Actions);
    }

    [Fact]
    public void Step_ActionLimit_PreservesTheFinalObservationAndDoesNotInventAWinner()
    {
        var session = new TrainingSession(TestWorld.Map());
        session.Apply(new("reset", MaxActions: 1));
        var frame = session.Apply(new("step", Action: 0));
        frame.Truncated.Should().BeTrue();
        frame.Terminated.Should().BeFalse();
        frame.Winner.Should().Be(-1);
        frame.Reward.Should().Be(0);
        frame.Observation.State.Should().HaveCount(ObservationEncoder.StateSize);
    }

    [Fact]
    public void Reset_TimeLimitDuringOpponentTurn_EncodesTheLearnersPerspective()
    {
        var session = new TrainingSession(TestWorld.Map());
        var frame = session.Apply(new("reset", Seed: 9, Seat: 1, MaxActions: 1));
        frame.Truncated.Should().BeTrue();
        frame.Player.Should().Be(1);
        var expected = TestWorld.Game(2, seed: 9);
        var opponent = new ExpertStrategy();
        while (expected.State.CurrentPlayer == 0) expected.Apply(0, opponent.Choose(GameObservation.From(expected)));
        frame.Actions.Should().BeGreaterThan(1);
        frame.Observation.State[42 * 8 + (int)Phase.Draft].Should().Be(1);
        frame.Observation.State[42 * 8 + 7 + 44].Should().BeGreaterThan(0);
        foreach (var territory in expected.State.Territories)
            frame.Observation.State[territory.Id * 8].Should().Be(territory.Owner == 1 ? 1 : 0);
    }

    [Fact]
    public void Encode_EqualBonusTrades_WithDifferentRemainingHands_AreDistinct()
    {
        var game = TestWorld.Game(2);
        var observation = GameObservation.From(game);
        GameCommand[] commands = [new() { Kind = CommandKind.Trade, Cards = [41, 6, 4], BonusTerritory = 4 },
            new() { Kind = CommandKind.Trade, Cards = [41, 9, 4], BonusTerritory = 4 }];
        var candidates = ObservationEncoder.Encode(observation, commands).Candidates;
        candidates[0].Take(24).Should().Equal(candidates[1].Take(24));
        candidates[0].Should().NotEqual(candidates[1]);
        candidates[0][24 + 6].Should().Be(1);
        candidates[1][24 + 6].Should().Be(0);
    }

    [Fact]
    public void Encode_OpponentCardIdentitiesAndDeckOrder_DoNotAffectInputs()
    {
        var game = TestWorld.Game(2);
        game.State.Players[1].Cards.AddRange([7, 8]);
        var observation = GameObservation.From(game);
        var before = ObservationEncoder.Encode(observation, ActionCatalog.Create(observation));
        game.State.Players[1].Cards.Clear();
        game.State.Players[1].Cards.AddRange([10, 11]);
        var deck = game.State.Deck.Reverse().ToArray();
        game.State.Deck.Clear();
        foreach (var card in deck) game.State.Deck.Enqueue(card);
        observation = GameObservation.From(game);
        JsonConvert.SerializeObject(ObservationEncoder.Encode(observation, ActionCatalog.Create(observation))).Should().Be(JsonConvert.SerializeObject(before));
    }

    [Fact]
    public void Reset_UnknownOpponent_IsRejected()
    {
        var session = new TrainingSession(TestWorld.Map());
        var action = () => session.Apply(new("reset", Opponent: "unknown"));
        action.Should().Throw<ArgumentException>();
    }
}
