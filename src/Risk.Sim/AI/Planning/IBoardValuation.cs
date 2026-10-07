namespace Risk.Sim.AI.Planning;

/// <summary>Corrects the hand-written score of a planned board, in the same units, from the evaluator's player perspective.</summary>
public interface IBoardValuation
{
    double Correction(PositionEvaluator evaluator, PlannerBoard board, double score);
}
