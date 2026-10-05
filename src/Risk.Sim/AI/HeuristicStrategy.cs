using Risk.Sim.Models;
namespace Risk.Sim.AI;

public sealed class HeuristicStrategy(bool cautious) : IPlayerStrategy
{
    public string Id => cautious ? "heuristic-easy" : "heuristic-normal";

    public GameCommand Choose(GameObservation observation)
    {
        var prepared = StrategicMoves.Prepare(observation);
        if (prepared != null) return prepared;
        var threshold = cautious ? 1.9 : 1.25;
        if (observation.ConqueredThisTurn) threshold += 0.4;
        var attack = StrategicMoves.Attacks(observation)
            .Where(a => a.From.Troops - 1 >= a.To.Troops * threshold)
            .OrderByDescending(a => a.Score).FirstOrDefault();
        return attack == null ? new() { Kind = CommandKind.EndAttack }
            : new() { Kind = CommandKind.Attack, From = attack.From.Id, To = attack.To.Id };
    }
}
