namespace Risk.Sim.AI;

public sealed record AttackCandidate(ObservedTerritory From, ObservedTerritory To, double Score);
