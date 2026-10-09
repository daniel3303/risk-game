using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// One recorded afterstate as a fixed row of little-endian floats; the duel and multiplayer layouts differ, and each knows how to
/// rebuild the mover's evaluator and board for training.
/// </summary>
public abstract class TrajectoryFormat
{
    /// <summary>Seeds are stored as floats, which hold every integer below this limit exactly.</summary>
    public const int SeedLimit = 1 << 24;
    public static TrajectoryFormat Duel { get; } = new DuelTrajectoryFormat();
    public static TrajectoryFormat Multiplayer { get; } = new MultiplayerTrajectoryFormat();
    public static TrajectoryFormat For(int players) => players == 2 ? Duel : Multiplayer;

    public abstract int Width { get; }
    public abstract int Score { get; }
    public abstract int Turn { get; }
    public abstract int Mover { get; }
    public abstract int Outcome { get; }
    public abstract int Seed { get; }
    public abstract BoardEncoding Encoding { get; }
    public abstract float[] Encode(GameObservation observation, PlannerBoard board, double score);
    /// <summary>Rebuilds the mover's evaluator and board; hidden card identities are irrelevant to the features.</summary>
    public abstract (PositionEvaluator Evaluator, PlannerBoard Board) Rebuild(WorldMap map, float[] row);

    public float[][] Read(IEnumerable<string> paths) => paths.SelectMany(path =>
    {
        var bytes = File.ReadAllBytes(path);
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, floats.Length * sizeof(float));
        return Enumerable.Range(0, floats.Length / Width).Select(r => floats.AsSpan(r * Width, Width).ToArray());
    }).ToArray();
}

/// <summary>93 floats: owners relative to the mover (0 mover, 1 rival), troops, conquered flag, mover cards, rival cards, trades, Master score, mover turn index, mover id, outcome, seed.</summary>
public sealed class DuelTrajectoryFormat : TrajectoryFormat
{
    public override int Width => 93;
    public override int Score => 88;
    public override int Turn => 89;
    public override int Mover => 90;
    public override int Outcome => 91;
    public override int Seed => 92;
    public override BoardEncoding Encoding => BoardEncoding.Duel;

    public override float[] Encode(GameObservation observation, PlannerBoard board, double score)
    {
        var row = new float[Width];
        var rival = observation.Players.First(p => p.Id != observation.Player && !p.Eliminated).Id;
        for (var id = 0; id < 42; id++)
        {
            row[id] = board.Owners[id] == observation.Player ? 0 : 1;
            row[42 + id] = board.Troops[id];
        }
        row[84] = board.Conquered ? 1 : 0;
        row[85] = observation.Cards.Length;
        row[86] = observation.Players.First(p => p.Id == rival).CardCount;
        row[87] = observation.Trades;
        row[Score] = (float)score;
        return row;
    }

    public override (PositionEvaluator Evaluator, PlannerBoard Board) Rebuild(WorldMap map, float[] row)
    {
        var owners = new int[42];
        var troops = new int[42];
        for (var id = 0; id < 42; id++) { owners[id] = (int)row[id]; troops[id] = (int)row[42 + id]; }
        var conquered = row[84] == 1;
        var ownCards = (int)row[85];
        var observation = new GameObservation(map, Phase.Attack, 0, 0, new int[ownCards],
            Enumerable.Range(0, 42).Select(id => new ObservedTerritory(id, owners[id], troops[id])).ToArray(), null, conquered,
            new GameOptions(CardMode.Fixed, SetupMode.Automatic), (int)row[87],
            [new ObservedPlayer(0, false, ownCards), new ObservedPlayer(1, false, (int)row[86])]);
        return (new PositionEvaluator(observation, ExpertTuning.Master), new PlannerBoard(owners, troops, conquered));
    }
}

/// <summary>
/// 104 floats: absolute owners, troops, each player's card count and eliminated flag (six slots), player count, mover, conquered
/// flag, trades, Ultimate's hand score, mover turn index, outcome, seed.
/// </summary>
public sealed class MultiplayerTrajectoryFormat : TrajectoryFormat
{
    private const int Players = 6;
    private const int Cards = 84;
    private const int Eliminated = Cards + Players;
    private const int Count = Eliminated + Players;
    public override int Width => 104;
    public override int Mover => Count + 1;
    public override int Score => Count + 4;
    public override int Turn => Count + 5;
    public override int Outcome => Count + 6;
    public override int Seed => Count + 7;
    public override BoardEncoding Encoding => BoardEncoding.Multiplayer;

    public override float[] Encode(GameObservation observation, PlannerBoard board, double score)
    {
        var row = new float[Width];
        for (var id = 0; id < 42; id++) { row[id] = board.Owners[id]; row[42 + id] = board.Troops[id]; }
        foreach (var player in observation.Players)
        {
            row[Cards + player.Id] = player.Id == observation.Player ? observation.Cards.Length : player.CardCount;
            row[Eliminated + player.Id] = player.Eliminated ? 1 : 0;
        }
        row[Count] = observation.Players.Length;
        row[Mover] = observation.Player;
        row[Count + 2] = board.Conquered ? 1 : 0;
        row[Count + 3] = observation.Trades;
        row[Score] = (float)score;
        return row;
    }

    public override (PositionEvaluator Evaluator, PlannerBoard Board) Rebuild(WorldMap map, float[] row)
    {
        var owners = new int[42];
        var troops = new int[42];
        for (var id = 0; id < 42; id++) { owners[id] = (int)row[id]; troops[id] = (int)row[42 + id]; }
        var count = (int)row[Count];
        var mover = (int)row[Mover];
        var players = Enumerable.Range(0, count).Select(id => new ObservedPlayer(id, row[Eliminated + id] == 1, (int)row[Cards + id])).ToArray();
        var conquered = row[Count + 2] == 1;
        var observation = new GameObservation(map, Phase.Attack, mover, 0, new int[(int)row[Cards + mover]],
            Enumerable.Range(0, 42).Select(id => new ObservedTerritory(id, owners[id], troops[id])).ToArray(), null, conquered,
            new GameOptions(CardMode.Fixed, SetupMode.Automatic), (int)row[Count + 3], players);
        return (new PositionEvaluator(observation, ExpertTuning.Ultimate), new PlannerBoard(owners, troops, conquered));
    }
}
