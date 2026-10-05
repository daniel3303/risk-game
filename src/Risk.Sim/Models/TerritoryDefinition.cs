namespace Risk.Sim.Models;

public sealed record TerritoryDefinition(int Id, string Key, string Name, string Continent, double X, double Z, int[] Neighbors, double[][] Shape);
