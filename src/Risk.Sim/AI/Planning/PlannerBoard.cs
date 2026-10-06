namespace Risk.Sim.AI.Planning;

public sealed class PlannerBoard(int[] owners, int[] troops, bool conquered)
{
    public int[] Owners { get; } = owners;
    public int[] Troops { get; } = troops;
    public bool Conquered { get; set; } = conquered;
    public PlannerBoard Copy() => new(Owners.ToArray(), Troops.ToArray(), Conquered);
    public static PlannerBoard From(GameObservation observation) => new(
        observation.Territories.Select(t => t.Owner).ToArray(), observation.Territories.Select(t => t.Troops).ToArray(), observation.ConqueredThisTurn);
}
