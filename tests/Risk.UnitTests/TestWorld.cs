using Risk.Sim;
using Risk.Sim.Models;
namespace Risk.UnitTests;

internal static class TestWorld
{
    public static WorldMap Map()
    {
        using var stream = typeof(TestWorld).Assembly.GetManifestResourceStream("Risk.UnitTests.classic.json");
        using var reader = new StreamReader(stream);
        return WorldMap.Parse(reader.ReadToEnd());
    }

    public static Game Game(int players = 3, GameOptions options = null, int seed = 123)
        => new(Map(), Enumerable.Range(0, players).Select(i => $"Commander {i}").ToArray(), options ?? new(), new SeededRandom(seed));

    public static void Board(Game game, int owner = 2)
    {
        foreach (var territory in game.State.Territories) { territory.Owner = owner; territory.Troops = 1; }
        game.State.CurrentPlayer = 0;
        game.State.Phase = Phase.Attack;
        game.State.Territories[0].Owner = 0;
        game.State.Territories[1].Owner = 1;
    }
}
