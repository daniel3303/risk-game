using Newtonsoft.Json;
using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.Learning;

public static class ActionCatalog
{
    public const int Capacity = 128;
    private const int FortifySources = 6;
    private const int FortifyTargets = 4;

    public static GameCommand[] Create(GameObservation observation)
    {
        var evaluator = new PositionEvaluator(observation);
        var board = PlannerBoard.From(observation);
        var actions = observation.Phase switch
        {
            Phase.Claim => observation.Territories.Where(t => t.Owner < 0).Select(t => new GameCommand { Kind = CommandKind.Claim, To = t.Id }),
            Phase.Setup => Placements(observation, 1),
            Phase.Draft => Trades(observation).Concat(observation.Cards.Length < 5 ? Placements(observation, observation.Reinforcements) : []),
            Phase.Attack => Attacks(observation),
            Phase.Occupy => Occupations(observation, evaluator, board),
            Phase.Fortify => Fortifications(observation, evaluator, board),
            _ => [],
        };
        var result = actions.DistinctBy(Key).ToArray();
        if (result.Length > Capacity) throw new InvalidOperationException("The training action catalogue exceeded its fixed capacity.");
        return result;
    }

    public static string Key(GameCommand command) => JsonConvert.SerializeObject(command with { Cards = command.Cards.Order().ToArray() });

    public static double BorderPriority(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board, int id) =>
        observation.Map.Territories[id].Neighbors.Where(n => board.Owners[n] != observation.Player)
            .Select(n => evaluator.Priority(board, id, n)).DefaultIfEmpty(0).Max() + board.Troops[id] * .1;

    private static IEnumerable<GameCommand> Placements(GameObservation observation, int available) =>
        observation.Territories.Where(t => t.Owner == observation.Player).SelectMany(t =>
            new[] { available, Math.Max(1, available / 2) }.Distinct().Where(count => count > 0 && count <= available)
                .Select(count => new GameCommand { Kind = CommandKind.Place, To = t.Id, Count = count }));

    private static IEnumerable<GameCommand> Attacks(GameObservation observation) =>
        observation.Territories.Where(t => t.Owner == observation.Player && t.Troops > 1)
            .SelectMany(from => observation.Map.Territories[from.Id].Neighbors.Where(to => observation.Territories[to].Owner >= 0 && observation.Territories[to].Owner != observation.Player)
                .Select(to => new GameCommand { Kind = CommandKind.Attack, From = from.Id, To = to }))
            .Append(new() { Kind = CommandKind.EndAttack });

    private static IEnumerable<GameCommand> Trades(GameObservation observation)
    {
        foreach (var set in Sets(observation.Cards))
        {
            var bonuses = set.Where(id => id < 42 && observation.Territories[id].Owner == observation.Player).ToArray();
            foreach (var id in bonuses.Length > 0 ? bonuses : new[] { -1 })
                yield return new() { Kind = CommandKind.Trade, Cards = set, BonusTerritory = id };
        }
    }

    private static IEnumerable<int[]> Sets(int[] cards)
    {
        for (var i = 0; i < cards.Length - 2; i++)
            for (var j = i + 1; j < cards.Length - 1; j++)
                for (var k = j + 1; k < cards.Length; k++)
                    if (CardRules.FixedBonus([cards[i], cards[j], cards[k]]) > 0) yield return [cards[i], cards[j], cards[k]];
    }

    private static IEnumerable<GameCommand> Occupations(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board)
    {
        var capture = observation.Capture;
        board.Owners[capture.To] = observation.Player;
        board.Troops[capture.From] -= capture.Maximum;
        board.Troops[capture.To] = capture.Maximum;
        var guarded = Math.Clamp(capture.Maximum + 1 - evaluator.Guard(board, capture.From), capture.Minimum, capture.Maximum);
        return new[] { capture.Minimum, (capture.Minimum + capture.Maximum) / 2, capture.Maximum, guarded }.Distinct()
            .Select(count => new GameCommand { Kind = CommandKind.Occupy, Count = count });
    }

    private static IEnumerable<GameCommand> Fortifications(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board)
    {
        yield return new() { Kind = CommandKind.EndTurn };
        var owned = observation.Territories.Where(t => t.Owner == observation.Player).ToArray();
        foreach (var from in owned.Where(t => t.Troops > 1).OrderByDescending(t => t.Troops).Take(FortifySources))
            foreach (var to in owned.Where(t => t.Id != from.Id && evaluator.Border(board, t.Id) && StrategicMoves.Connected(observation, from.Id, t.Id))
                .OrderByDescending(t => BorderPriority(observation, evaluator, board, t.Id)).Take(FortifyTargets))
                foreach (var count in FortifyAmounts(evaluator, board, from.Id))
                    yield return new() { Kind = CommandKind.Fortify, From = from.Id, To = to.Id, Count = count };
    }

    private static IEnumerable<int> FortifyAmounts(PositionEvaluator evaluator, PlannerBoard board, int from)
    {
        var troops = board.Troops[from];
        var guarded = troops - (evaluator.Border(board, from) ? Math.Min(troops, evaluator.Guard(board, from)) : 1);
        return new[] { troops - 1, Math.Max(1, (troops - 1) / 2), guarded }.Distinct().Where(count => count > 0);
    }
}
