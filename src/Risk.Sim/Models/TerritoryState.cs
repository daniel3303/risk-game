namespace Risk.Sim.Models;

public sealed class TerritoryState(int id)
{
    public int Id { get; } = id;
    public int Owner { get; set; } = -1;
    public int Troops { get; set; }
}
