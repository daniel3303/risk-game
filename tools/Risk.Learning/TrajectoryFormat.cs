using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// One recorded afterstate as 93 little-endian floats: owners relative to the mover (0 mover, 1 rival), troops,
/// conquered flag, mover cards, rival cards, trades, Master score, mover turn index, mover id, outcome, seed.
/// </summary>
public static class TrajectoryFormat
{
    public const int Width = 93;
    public const int Score = 88;
    public const int Turn = 89;
    public const int Mover = 90;
    public const int Outcome = 91;
    public const int Seed = 92;
    /// <summary>Seeds are stored as floats, which hold every integer below this limit exactly.</summary>
    public const int SeedLimit = 1 << 24;

    public static float[] Encode(GameObservation observation, PlannerBoard board, double score)
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

    /// <summary>Rebuilds the mover's evaluator and board; hidden card identities are irrelevant to the features.</summary>
    public static (PositionEvaluator Evaluator, PlannerBoard Board) Rebuild(WorldMap map, float[] row)
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

    public static float[][] Read(IEnumerable<string> paths) => paths.SelectMany(path =>
    {
        var bytes = File.ReadAllBytes(path);
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, floats.Length * sizeof(float));
        return Enumerable.Range(0, floats.Length / Width).Select(r => floats.AsSpan(r * Width, Width).ToArray());
    }).ToArray();
}
