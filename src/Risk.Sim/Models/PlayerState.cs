namespace Risk.Sim.Models;

public sealed class PlayerState(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public bool Eliminated { get; set; }
    public int SetupTroops { get; set; }
    public List<int> Cards { get; } = [];
}
