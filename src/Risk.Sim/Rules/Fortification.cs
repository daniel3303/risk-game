using Risk.Sim.Models;
namespace Risk.Sim.Rules;

public static class Fortification
{
    public static bool Connected(WorldMap map, GameState state, int from, int to, int owner)
    {
        if (state.Territories[from].Owner != owner || state.Territories[to].Owner != owner) return false;
        var visited = new HashSet<int> { from };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.TryDequeue(out var current))
        {
            if (current == to) return true;
            foreach (var neighbor in map.Territories[current].Neighbors)
                if (state.Territories[neighbor].Owner == owner && visited.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return false;
    }

    public static void Move(WorldMap map, GameState state, TerritoryState from, TerritoryState to, int count)
    {
        if (from.Id == to.Id || count < 1 || count >= from.Troops || !Connected(map, state, from.Id, to.Id, state.CurrentPlayer))
            throw new RuleException("Move troops through connected friendly territories, leaving one behind.");
        from.Troops -= count;
        to.Troops += count;
    }
}
