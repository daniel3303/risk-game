using Risk.Sim.AI;
using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.Learning;

public static class ObservationEncoder
{
    public const int Version = 2;
    public const int StateSize = 481;
    public const int CandidateSize = 68;
    private const int TerritoryCount = 42;
    private const int CardCount = 44;
    private const double ArmyScale = 1001;

    public static EncodedObservation Encode(GameObservation observation, GameCommand[] commands)
    {
        var evaluator = new PositionEvaluator(observation);
        var board = PlannerBoard.From(observation);
        var state = TerritoryFeatures(observation, evaluator, board).Concat(GlobalFeatures(observation, evaluator, board)).ToArray();
        if (state.Length != StateSize) throw new InvalidOperationException("Observation schema mismatch.");
        return new(state, commands.Select(command => Candidate(observation, evaluator, board, command)).ToArray());
    }

    private static IEnumerable<float> TerritoryFeatures(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board)
    {
        foreach (var t in observation.Territories)
        {
            var neighbors = observation.Map.Territories[t.Id].Neighbors;
            var region = evaluator.Regions.First(r => r.Contains(t.Id));
            yield return t.Owner == observation.Player ? 1 : 0;
            yield return t.Owner >= 0 && t.Owner != observation.Player ? 1 : 0;
            yield return t.Owner < 0 ? 1 : 0;
            yield return Army(t.Troops);
            yield return (float)neighbors.Count(n => board.Owners[n] == observation.Player) / neighbors.Length;
            yield return (float)region.Count(n => board.Owners[n] == observation.Player) / region.Length;
            yield return observation.Cards.Contains(t.Id) ? 1 : 0;
            yield return evaluator.CompleteRegion(board, t.Id) ? 1 : 0;
        }
    }

    private static IEnumerable<float> GlobalFeatures(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board)
    {
        var owned = observation.Territories.Where(t => t.Owner == observation.Player).ToArray();
        var total = observation.Territories.Sum(t => (double)t.Troops);
        var features = Enumerable.Range(0, 7).Select(p => p == (int)observation.Phase ? 1f : 0f)
            .Concat(Enumerable.Range(0, CardCount).Select(id => observation.Cards.Contains(id) ? 1f : 0f))
            .Concat(new[] { Army(observation.Reinforcements), Army(observation.Trades), observation.ConqueredThisTurn ? 1f : 0f,
                owned.Length / (float)TerritoryCount, (float)(owned.Sum(t => (double)t.Troops) / Math.Max(1, total)),
                observation.Cards.Length / 5f, (observation.Players?.Where(p => p.Id != observation.Player).Sum(p => p.CardCount) ?? 0) / 5f,
                Army(evaluator.Income(board, observation.Player)) });
        return features.Concat(Enumerable.Range(0, TerritoryCount).Select(id => observation.Capture?.From == id ? 1f : 0f))
            .Concat(Enumerable.Range(0, TerritoryCount).Select(id => observation.Capture?.To == id ? 1f : 0f))
            .Concat(new[] { Army(observation.Capture?.Minimum ?? 0), Army(observation.Capture?.Maximum ?? 0) });
    }

    private static float[] Candidate(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board, GameCommand command)
    {
        var from = command.Kind == CommandKind.Occupy ? observation.Capture.From : command.From;
        var to = command.Kind == CommandKind.Occupy ? observation.Capture.To : command.Kind == CommandKind.Trade ? command.BonusTerritory : command.To;
        var source = from >= 0 ? board.Troops[from] : 0;
        var target = to >= 0 ? board.Troops[to] : 0;
        var odds = command.Kind == CommandKind.Attack ? BattleOdds.Estimate(source - 1, target) : default;
        var available = command.Kind == CommandKind.Place ? observation.Reinforcements : command.Kind == CommandKind.Occupy ? observation.Capture.Maximum : Math.Max(0, source - 1);
        float[] values = [(from + 1) / 43f, (to + 1) / 43f, Army(command.Count), command.Count / (float)Math.Max(1, available),
            Army(source), Army(target), NeighborFraction(observation, board, from, false), NeighborFraction(observation, board, to, true),
            RegionFraction(evaluator, board, from), RegionFraction(evaluator, board, to), to >= 0 ? observation.Map.Continents.First(c => c.Id == observation.Map.Territories[to].Continent).Bonus / 7f : 0,
            (float)odds.WinChance, Army(odds.SurvivorsOnWin), to >= 0 ? (float)Math.Tanh(ActionCatalog.BorderPriority(observation, evaluator, board, to) / 10) : 0,
            from >= 0 ? evaluator.Guard(board, from) / (float)Math.Max(1, source) : 0, command.Kind == CommandKind.Trade ? CardRules.FixedBonus(command.Cards) / 10f : 0];
        var features = Enumerable.Range(0, 8).Select(kind => kind == (int)command.Kind ? 1f : 0f).Concat(values)
            .Concat(Enumerable.Range(0, CardCount).Select(id => command.Cards.Contains(id) ? 1f : 0f)).ToArray();
        if (features.Length != CandidateSize || features.Any(f => !float.IsFinite(f))) throw new InvalidOperationException("Candidate schema mismatch.");
        return features;
    }

    private static float NeighborFraction(GameObservation observation, PlannerBoard board, int id, bool friendly) => id < 0 ? 0 :
        (float)observation.Map.Territories[id].Neighbors.Count(n => (board.Owners[n] == observation.Player) == friendly) / observation.Map.Territories[id].Neighbors.Length;

    private static float RegionFraction(PositionEvaluator evaluator, PlannerBoard board, int id) => id < 0 ? 0 :
        evaluator.Regions.Where(r => r.Contains(id)).Select(r => (float)r.Count(n => board.Owners[n] == evaluator.Player) / r.Length).Single();

    private static float Army(double value) => (float)(Math.Log(1 + Math.Max(0, value)) / Math.Log(ArmyScale));
}
