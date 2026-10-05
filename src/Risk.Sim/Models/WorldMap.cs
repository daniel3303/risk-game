using Newtonsoft.Json;
namespace Risk.Sim.Models;

public sealed class WorldMap
{
    public string Name { get; init; }
    public ContinentDefinition[] Continents { get; init; } = [];
    public TerritoryDefinition[] Territories { get; init; } = [];

    public static WorldMap Parse(string json)
    {
        var map = JsonConvert.DeserializeObject<WorldMap>(json) ?? throw new ArgumentException("Map is missing.");
        if (map.Territories.Length != 42 || map.Continents.Length != 6)
            throw new ArgumentException("Classic requires 42 territories and six continents.");
        foreach (var territory in map.Territories)
        {
            if (territory.Id < 0 || territory.Id >= 42 || map.Territories[territory.Id] != territory)
                throw new ArgumentException("Territory IDs must be consecutive.");
            if (territory.Neighbors.Length == 0 || territory.Neighbors.Distinct().Count() != territory.Neighbors.Length)
                throw new ArgumentException("Territory borders must be unique.");
            if (!map.Continents.Any(c => c.Id == territory.Continent)) throw new ArgumentException("Unknown continent.");
            foreach (var neighbor in territory.Neighbors)
                if (neighbor == territory.Id || neighbor < 0 || neighbor >= 42 || !map.Territories[neighbor].Neighbors.Contains(territory.Id))
                    throw new ArgumentException("Borders must be valid and symmetric.");
        }
        return map;
    }
}
