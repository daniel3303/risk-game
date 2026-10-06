using Risk.Sim.Models;
namespace Risk.Sim.AI.Planning;

public sealed record PlanNode(PlannerBoard Board, double Value, double Gain, double Probability, GameCommand First);
