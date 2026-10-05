using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI;

public static class StrategicMoves
{
    public static GameCommand Prepare(GameObservation observation)
    {
        if (observation.Phase == Phase.Claim)
        {
            var unclaimed = observation.Territories.Where(t => t.Owner == -1).OrderByDescending(t => ContinentValue(observation, t.Id)).First();
            return new() { Kind = CommandKind.Claim, To = unclaimed.Id };
        }
        if (observation.Phase == Phase.Occupy) return new() { Kind = CommandKind.Occupy, Count = observation.Capture.Maximum };
        if (observation.Phase == Phase.Fortify) return Fortify(observation);
        if (observation.Phase is not (Phase.Draft or Phase.Setup)) return null;
        var set = CardRules.FindSet(observation.Cards);
        if (observation.Phase == Phase.Draft && set.Length > 0) return new() { Kind = CommandKind.Trade, Cards = set };
        var border = observation.Territories.Where(t => t.Owner == observation.Player)
            .OrderByDescending(t => DraftScore(observation, t)).First();
        return new() { Kind = CommandKind.Place, To = border.Id, Count = observation.Phase == Phase.Setup ? 1 : observation.Reinforcements };
    }

    public static IEnumerable<AttackCandidate> Attacks(GameObservation observation)
    {
        foreach (var from in observation.Territories.Where(t => t.Owner == observation.Player && t.Troops > 1))
            foreach (var target in observation.Map.Territories[from.Id].Neighbors.Select(id => observation.Territories[id]).Where(t => t.Owner != observation.Player))
                yield return new(from, target, (from.Troops - 1.0) / target.Troops + ContinentValue(observation, target.Id));
    }

    private static double DraftScore(GameObservation o, ObservedTerritory territory)
    {
        var enemies = o.Map.Territories[territory.Id].Neighbors.Select(id => o.Territories[id]).Where(t => t.Owner != o.Player).ToArray();
        if (enemies.Length == 0) return -1;
        return territory.Troops + ContinentValue(o, territory.Id) * 5 - enemies.Min(t => t.Troops) * 0.3;
    }

    private static double ContinentValue(GameObservation o, int id)
    {
        var continent = o.Map.Territories[id].Continent;
        var region = o.Map.Territories.Where(t => t.Continent == continent).ToArray();
        var owned = region.Count(t => o.Territories[t.Id].Owner == o.Player);
        return (owned + 1.0) / region.Length + 1.0 / region.Length;
    }

    private static GameCommand Fortify(GameObservation o)
    {
        var owned = o.Territories.Where(t => t.Owner == o.Player).ToArray();
        var borders = owned.Where(t => o.Map.Territories[t.Id].Neighbors.Any(n => o.Territories[n].Owner != o.Player)).ToArray();
        foreach (var from in owned.Where(t => t.Troops > 1).OrderByDescending(t => t.Troops))
        {
            if (borders.Any(t => t.Id == from.Id)) continue;
            var target = borders.OrderByDescending(t => DraftScore(o, t)).FirstOrDefault(t => Connected(o, from.Id, t.Id));
            if (target != null) return new() { Kind = CommandKind.Fortify, From = from.Id, To = target.Id, Count = from.Troops - 1 };
        }
        return new() { Kind = CommandKind.EndTurn };
    }

    private static bool Connected(GameObservation o, int from, int to)
    {
        var visited = new HashSet<int> { from };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.TryDequeue(out var current))
        {
            if (current == to) return true;
            foreach (var neighbor in o.Map.Territories[current].Neighbors)
                if (o.Territories[neighbor].Owner == o.Player && visited.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return false;
    }
}
