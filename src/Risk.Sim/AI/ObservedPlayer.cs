namespace Risk.Sim.AI;

/// <param name="Human">Whether a person plays the seat; bots may weigh such a rival apart from other bots.</param>
public sealed record ObservedPlayer(int Id, bool Eliminated, int CardCount, bool Human = false);
