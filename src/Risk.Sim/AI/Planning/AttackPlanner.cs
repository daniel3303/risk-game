using Risk.Sim.Models;
namespace Risk.Sim.AI.Planning;

public sealed class AttackPlanner(PositionEvaluator evaluator)
{
    private const int BeamWidth = 8;
    private const int Branches = 10;
    private const int MaxDepth = 6;
    public const int SearchBudget = 512;

    public PlanNode Search(PlannerBoard board, int budget = SearchBudget)
    {
        var root = new PlanNode(board, evaluator.Evaluate(board), 0, 1, null);
        var best = root;
        var frontier = new[] { root };
        var expanded = 0;
        for (var depth = 0; depth < MaxDepth && frontier.Length > 0 && expanded < budget; depth++)
        {
            var next = new List<PlanNode>();
            foreach (var node in frontier)
                foreach (var action in Candidates(node.Board).Take(Branches))
                    foreach (var child in Expand(node, action))
                    {
                        if (expanded++ >= budget) return best;
                        next.Add(child);
                        if (child.Gain > best.Gain + 1e-8) best = child;
                    }
            frontier = next.OrderByDescending(n => n.Gain).Take(BeamWidth).ToArray();
        }
        return best;
    }

    public IEnumerable<GameCommand> Candidates(PlannerBoard board) => board.Owners.Select((owner, id) => id)
        .Where(id => board.Owners[id] == evaluator.Player && board.Troops[id] > 1)
        .SelectMany(from => evaluator.Observation.Map.Territories[from].Neighbors.Where(to => board.Owners[to] >= 0 && board.Owners[to] != evaluator.Player)
            .Select(to => new GameCommand { Kind = CommandKind.Attack, From = from, To = to }))
        .OrderByDescending(action => evaluator.Priority(board, action.From, action.To));

    private IEnumerable<PlanNode> Expand(PlanNode node, GameCommand action)
    {
        var available = node.Board.Troops[action.From] - 1;
        var defenders = node.Board.Troops[action.To];
        var odds = BattleOdds.Estimate(available, defenders);
        var threshold = evaluator.Duel || !node.Board.Conquered ? .6 : .78;
        if (odds.WinChance < threshold) yield break;
        var success = node.Board.Copy();
        success.Owners[action.To] = evaluator.Player;
        success.Troops[action.From] = 1;
        success.Troops[action.To] = Math.Max(1, (int)Math.Floor(odds.SurvivorsOnWin));
        success.Conquered = true;
        var failure = node.Board.Copy();
        failure.Troops[action.From] = 1;
        failure.Troops[action.To] = Math.Max(1, (int)Math.Ceiling(odds.DefendersOnLoss));
        var failureValue = evaluator.Evaluate(failure);
        // The final maximum-dice roll can require three armies to enter the capture.
        var minimum = Math.Min(3, success.Troops[action.To]);
        foreach (var occupation in Occupations(success, action.From, action.To, minimum))
        {
            var value = evaluator.Evaluate(occupation);
            var gain = node.Gain + node.Probability * (odds.WinChance * (value - node.Value) + (1 - odds.WinChance) * (failureValue - node.Value));
            yield return new(occupation, value, gain, node.Probability * odds.WinChance, node.First ?? action);
        }
    }

    public IEnumerable<PlannerBoard> Occupations(PlannerBoard board, int from, int to, int minimum)
    {
        yield return board;
        var guard = Math.Min(evaluator.Guard(board, from), board.Troops[from] + board.Troops[to] - minimum);
        if (guard <= board.Troops[from]) yield break;
        var guarded = board.Copy();
        guarded.Troops[to] -= guard - guarded.Troops[from];
        guarded.Troops[from] = guard;
        yield return guarded;
    }
}
