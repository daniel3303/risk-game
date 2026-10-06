using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI;

public sealed class ExpertStrategy : IPlayerStrategy
{
    public string Id => "expert-turn-planner";

    public GameCommand Choose(GameObservation observation)
    {
        var evaluator = new PositionEvaluator(observation);
        var board = PlannerBoard.From(observation);
        var planner = new AttackPlanner(evaluator);
        return observation.Phase switch
        {
            Phase.Claim => Claim(evaluator, board),
            Phase.Setup => Deploy(evaluator, planner, board, 1),
            Phase.Draft => Trade(observation, evaluator, board) ?? Deploy(evaluator, planner, board, observation.Reinforcements),
            Phase.Attack => planner.Search(board).First ?? new() { Kind = CommandKind.EndAttack },
            Phase.Occupy => Occupy(observation, evaluator, planner, board),
            Phase.Fortify => Fortify(evaluator, planner, board),
            _ => throw new RuleException("The game has ended."),
        };
    }

    private static GameCommand Claim(PositionEvaluator evaluator, PlannerBoard board)
    {
        var target = board.Owners.Select((owner, id) => id).Where(id => board.Owners[id] < 0)
            .OrderByDescending(id => ClaimValue(evaluator, board, id)).First();
        return new() { Kind = CommandKind.Claim, To = target };
    }

    private static double ClaimValue(PositionEvaluator evaluator, PlannerBoard board, int id)
    {
        var region = evaluator.Regions.First(r => r.Contains(id));
        var bonus = evaluator.Observation.Map.Continents[Array.IndexOf(evaluator.Regions, region)].Bonus;
        var friendly = region.Count(n => board.Owners[n] == evaluator.Player);
        var unclaimed = region.Count(n => board.Owners[n] < 0);
        var adjacent = evaluator.Observation.Map.Territories[id].Neighbors.Count(n => board.Owners[n] == evaluator.Player);
        return (friendly * 3.0 + bonus + 2) / region.Length + adjacent * .6 + (unclaimed == 1 ? bonus : 0)
            - evaluator.Observation.Map.Territories[id].Neighbors.Length * .1;
    }

    private static GameCommand Trade(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board)
    {
        int[] best = null;
        var value = double.NegativeInfinity;
        var cards = observation.Cards;
        for (var i = 0; i < cards.Length - 2; i++)
            for (var j = i + 1; j < cards.Length - 1; j++)
                for (var k = j + 1; k < cards.Length; k++)
                {
                    int[] set = [cards[i], cards[j], cards[k]];
                    var bonus = CardRules.FixedBonus(set);
                    if (bonus == 0) continue;
                    var owned = set.Where(id => id < 42 && board.Owners[id] == evaluator.Player).ToArray();
                    var score = ((observation.Options?.Cards ?? CardMode.Fixed) == CardMode.Fixed ? bonus : 0) + (owned.Length > 0 ? 2 : 0);
                    if (score <= value) continue;
                    value = score; best = set;
                }
        if (best == null) return null;
        var territory = best.Where(id => id < 42 && board.Owners[id] == evaluator.Player)
            .OrderByDescending(id => evaluator.Border(board, id) ? board.Troops[id] : -1).DefaultIfEmpty(-1).First();
        return new() { Kind = CommandKind.Trade, Cards = best, BonusTerritory = territory };
    }

    private static GameCommand Deploy(PositionEvaluator evaluator, AttackPlanner planner, PlannerBoard board, int count)
    {
        var owned = board.Owners.Select((owner, id) => id).Where(id => board.Owners[id] == evaluator.Player).ToArray();
        var borders = owned.Where(id => evaluator.Border(board, id)).OrderByDescending(id => DeployPriority(evaluator, board, id)).Take(8).ToArray();
        var best = owned[0];
        var value = double.NegativeInfinity;
        foreach (var id in borders)
        {
            var placed = board.Copy(); placed.Troops[id] += count;
            var plan = planner.Search(placed, AttackPlanner.SearchBudget / Math.Max(1, borders.Length));
            var score = evaluator.Evaluate(placed) + plan.Gain;
            if (score <= value) continue;
            value = score; best = id;
        }
        return new() { Kind = CommandKind.Place, To = best, Count = count };
    }

    private static double DeployPriority(PositionEvaluator evaluator, PlannerBoard board, int id) =>
        evaluator.Observation.Map.Territories[id].Neighbors.Where(n => board.Owners[n] != evaluator.Player)
            .Select(n => evaluator.Priority(board, id, n)).DefaultIfEmpty(0).Max() + board.Troops[id] * .1;

    private static GameCommand Occupy(GameObservation observation, PositionEvaluator evaluator, AttackPlanner planner, PlannerBoard board)
    {
        var capture = observation.Capture;
        board.Owners[capture.To] = evaluator.Player;
        board.Troops[capture.From] -= capture.Maximum;
        board.Troops[capture.To] = capture.Maximum;
        board.Conquered = true;
        var best = planner.Occupations(board, capture.From, capture.To, capture.Minimum)
            .OrderByDescending(b => evaluator.Evaluate(b) + planner.Search(b, AttackPlanner.SearchBudget / 2).Gain).First();
        return new() { Kind = CommandKind.Occupy, Count = best.Troops[capture.To] };
    }

    private static GameCommand Fortify(PositionEvaluator evaluator, AttackPlanner planner, PlannerBoard board)
    {
        board.Conquered = false;
        var owned = board.Owners.Select((owner, id) => id).Where(id => board.Owners[id] == evaluator.Player).ToArray();
        var value = evaluator.Evaluate(board) + planner.Search(board, 64).Gain;
        var best = new GameCommand { Kind = CommandKind.EndTurn };
        foreach (var from in owned.Where(id => board.Troops[id] > 1).OrderByDescending(id => board.Troops[id]).Take(6))
            foreach (var to in owned.Where(id => id != from && evaluator.Border(board, id) && StrategicMoves.Connected(evaluator.Observation, from, id)).OrderByDescending(id => DeployPriority(evaluator, board, id)).Take(4))
            {
                var count = board.Troops[from] - (evaluator.Border(board, from) ? Math.Min(board.Troops[from], evaluator.Guard(board, from)) : 1);
                if (count < 1) continue;
                var moved = board.Copy(); moved.Troops[from] -= count; moved.Troops[to] += count;
                var score = evaluator.Evaluate(moved) + planner.Search(moved, 32).Gain;
                if (score <= value + 1e-8) continue;
                value = score; best = new() { Kind = CommandKind.Fortify, From = from, To = to, Count = count };
            }
        return best;
    }

}
