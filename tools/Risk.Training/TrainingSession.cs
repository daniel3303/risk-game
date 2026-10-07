using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Risk.Training.Models;
namespace Risk.Training;

public sealed class TrainingSession(WorldMap map)
{
    private const int OpponentSafetyActions = 1000;
    private Game game;
    private IPlayerStrategy opponent;
    private readonly ExpertStrategy teacher = new();
    private GameCommand[] commands = [];
    private int seat;
    private int actions;
    private int maxRounds;
    private int maxActions;

    public TrainingFrame Apply(TrainingRequest request) => request.Operation switch
    {
        "reset" => Reset(request),
        "step" => Step(request.Action),
        "teacher" => Frame(TeacherIndex()),
        _ => throw new ArgumentException("Use reset, step, or teacher."),
    };

    private TrainingFrame Reset(TrainingRequest request)
    {
        if (request.Seed < 0 || request.Seat is < 0 or > 1 || request.MaxRounds is < 1 or > 1000 || request.MaxActions is < 1 or > 100000)
            throw new ArgumentException("Choose a nonnegative seed, seat 0 or 1, and bounded round/action limits.");
        opponent = request.Opponent == "external" ? null : StrategyCatalog.Create(Difficulty(request.Opponent), unchecked(request.Seed * 1000003 + (1 - request.Seat) * 7919));
        seat = request.Seat; maxRounds = request.MaxRounds; maxActions = request.MaxActions; actions = 0;
        game = new(map, ["Seat 0", "Seat 1"], new(CardMode.Fixed, SetupMode.Automatic), new SeededRandom(request.Seed));
        AdvanceOpponent();
        return Frame();
    }

    private TrainingFrame Step(int action)
    {
        RequireGame();
        if (Done || action < 0 || action >= commands.Length) throw new ArgumentException("Choose a valid action index from the current frame.");
        RequireSafetyBudget();
        game.Apply(game.State.CurrentPlayer, commands[action]);
        actions++;
        AdvanceOpponent();
        return Frame();
    }

    private void AdvanceOpponent()
    {
        while (!Done && opponent != null && game.State.CurrentPlayer != seat)
        {
            RequireSafetyBudget();
            game.Apply(game.State.CurrentPlayer, opponent.Choose(GameObservation.From(game)));
            actions++;
        }
    }

    private TrainingFrame Frame(int teacherIndex = -1)
    {
        RequireGame();
        var observation = GameObservation.From(game);
        if (Done && observation.Player != seat)
            observation = observation with { Player = seat, Cards = game.State.Players[seat].Cards.ToArray() };
        if (!Done)
        {
            commands = ActionCatalog.Create(observation);
            if (commands.Length == 0) throw new InvalidOperationException("An active training state has no actions.");
        }
        else commands = [];
        // Preserve the final board for value bootstrapping at time limits.
        var encoded = ObservationEncoder.Encode(observation, commands);
        var terminated = game.State.Phase == Phase.Finished;
        return new(encoded, observation.Player, terminated, !terminated && Done,
            terminated ? game.State.Winner == seat ? 1 : -1 : 0, game.State.Winner, game.State.Round, actions, teacherIndex,
            teacherIndex >= 0 ? commands[teacherIndex] : null);
    }

    private int TeacherIndex()
    {
        RequireGame();
        if (Done) throw new ArgumentException("The episode has ended.");
        var command = teacher.Choose(GameObservation.From(game));
        var index = Array.FindIndex(commands, candidate => ActionCatalog.Key(candidate) == ActionCatalog.Key(command));
        if (index < 0) throw new InvalidOperationException("Expert's decision is missing from the training catalogue.");
        return index;
    }

    // A truncated observation must be an actual learner decision state for critic bootstrapping.
    private bool Done => game.State.Phase == Phase.Finished ||
        game.State.CurrentPlayer == seat && (game.State.Round > maxRounds || actions >= maxActions);
    private void RequireSafetyBudget()
    {
        if (actions >= maxActions + OpponentSafetyActions)
            throw new InvalidOperationException("The opponent exceeded the safety budget before returning a learner decision.");
    }
    private void RequireGame() { if (game == null) throw new ArgumentException("Reset before requesting a decision."); }
    private static BotDifficulty Difficulty(string value) => Enum.GetNames<BotDifficulty>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
        ? Enum.Parse<BotDifficulty>(value, true) : throw new ArgumentException("Use easy, normal, hard, expert, master, ultimate, or external.");
}
