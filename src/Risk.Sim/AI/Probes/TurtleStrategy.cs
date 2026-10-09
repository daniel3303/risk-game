using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI.Probes;

/// <summary>
/// Benchmark opponent imitating a human turtle: it secures one small continent, keeps one stack on that continent's border,
/// takes one cheap territory per turn for a card, trades every set, and once its army is half again the strongest rival's
/// it campaigns with Expert's planner. It is a probe for the Arena, not a lobby difficulty.
/// </summary>
public sealed class TurtleStrategy : IPlayerStrategy
{
    private const double FarmOdds = .9;
    private const double HomeOdds = .8;
    private const double StrikeRatio = 1.5;
    private const int StrikeArmy = 30;
    private readonly ExpertStrategy striker = new();
    public string Id => "probe-turtle";

    public GameCommand Choose(GameObservation observation)
    {
        var evaluator = new PositionEvaluator(observation);
        var board = PlannerBoard.From(observation);
        if (observation.Phase is (Phase.Draft or Phase.Attack or Phase.Occupy or Phase.Fortify) && Striking(evaluator, board)) return striker.Choose(observation);
        var home = Home(evaluator, board);
        return observation.Phase switch
        {
            Phase.Claim => new() { Kind = CommandKind.Claim, To = Claim(evaluator, board, home) },
            Phase.Setup => new() { Kind = CommandKind.Place, To = Fortress(evaluator, board, home), Count = 1 },
            Phase.Draft => Trade(observation, evaluator, board, home) ?? new() { Kind = CommandKind.Place, To = Fortress(evaluator, board, home), Count = observation.Reinforcements },
            Phase.Attack => Attack(observation, evaluator, board, home) ?? new() { Kind = CommandKind.EndAttack },
            Phase.Occupy => new() { Kind = CommandKind.Occupy, Count = home.Contains(observation.Capture.To) ? observation.Capture.Maximum : observation.Capture.Minimum },
            Phase.Fortify => Fortify(evaluator, board, home),
            _ => throw new RuleException("The game has ended."),
        };
    }

    /// <summary>The continent to hold: the best-owned share, preferring small continents the way human turtles do.</summary>
    private static int[] Home(PositionEvaluator evaluator, PlannerBoard board) => evaluator.Regions
        .OrderByDescending(region => region.Count(id => board.Owners[id] == evaluator.Player) / (double)region.Length + 1.0 / region.Length)
        .ThenBy(region => region.Length).First();

    private static int Claim(PositionEvaluator evaluator, PlannerBoard board, int[] home)
    {
        var free = Enumerable.Range(0, board.Owners.Length).Where(id => board.Owners[id] < 0).ToArray();
        var preferred = free.Where(home.Contains).ToArray();
        if (preferred.Length > 0) return preferred.OrderByDescending(id => Adjacent(evaluator, board, id)).First();
        // Once the home continent is taken, claim a neighbour of the fortress or the territory in the smallest open continent.
        return free.OrderByDescending(id => Adjacent(evaluator, board, id)).ThenBy(id => evaluator.Regions.First(r => r.Contains(id)).Length).First();
    }

    private static int Adjacent(PositionEvaluator evaluator, PlannerBoard board, int id) =>
        evaluator.Observation.Map.Territories[id].Neighbors.Count(n => board.Owners[n] == evaluator.Player);

    /// <summary>The single stack: the strongest external border of the home continent (the most threatened one on ties), or the strongest border anywhere.</summary>
    private static int Fortress(PositionEvaluator evaluator, PlannerBoard board, int[] home)
    {
        var owned = Enumerable.Range(0, board.Owners.Length).Where(id => board.Owners[id] == evaluator.Player).ToArray();
        var gates = owned.Where(id => home.Contains(id) && evaluator.Observation.Map.Territories[id].Neighbors.Any(n => !home.Contains(n))).ToArray();
        if (gates.Length > 0) return gates.OrderByDescending(id => board.Troops[id]).ThenByDescending(id => Threat(evaluator, board, id)).First();
        var borders = owned.Where(id => evaluator.Border(board, id)).ToArray();
        return (borders.Length > 0 ? borders : owned).OrderByDescending(id => board.Troops[id]).First();
    }

    private static int Threat(PositionEvaluator evaluator, PlannerBoard board, int id) =>
        evaluator.Observation.Map.Territories[id].Neighbors.Where(n => board.Owners[n] >= 0 && board.Owners[n] != evaluator.Player).Select(n => board.Troops[n]).DefaultIfEmpty(0).Max();

    private static GameCommand Trade(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board, int[] home)
    {
        var cards = observation.Cards;
        int[] best = null;
        var bonus = 0;
        for (var i = 0; i < cards.Length - 2; i++)
            for (var j = i + 1; j < cards.Length - 1; j++)
                for (var k = j + 1; k < cards.Length; k++)
                {
                    int[] set = [cards[i], cards[j], cards[k]];
                    var value = CardRules.FixedBonus(set);
                    if (value <= bonus) continue;
                    bonus = value; best = set;
                }
        if (best == null) return null;
        var fortress = Fortress(evaluator, board, home);
        var territory = best.Where(id => id < 42 && board.Owners[id] == evaluator.Player).OrderByDescending(id => id == fortress ? 1 : 0).DefaultIfEmpty(-1).First();
        return new() { Kind = CommandKind.Trade, Cards = best, BonusTerritory = territory };
    }

    /// <summary>Campaign only with a real army at least half again as large as the strongest rival's.</summary>
    private static bool Striking(PositionEvaluator evaluator, PlannerBoard board)
    {
        var armies = new Dictionary<int, int>();
        for (var id = 0; id < board.Owners.Length; id++)
            if (board.Owners[id] >= 0) armies[board.Owners[id]] = armies.GetValueOrDefault(board.Owners[id]) + board.Troops[id];
        var own = armies.GetValueOrDefault(evaluator.Player);
        var rival = armies.Where(a => a.Key != evaluator.Player).Select(a => a.Value).DefaultIfEmpty(0).Max();
        return own >= StrikeArmy && own >= StrikeRatio * rival;
    }

    private static GameCommand Attack(GameObservation observation, PositionEvaluator evaluator, PlannerBoard board, int[] home)
    {
        var options = Enumerable.Range(0, board.Owners.Length).Where(from => board.Owners[from] == evaluator.Player && board.Troops[from] > 1)
            .SelectMany(from => observation.Map.Territories[from].Neighbors.Where(to => board.Owners[to] >= 0 && board.Owners[to] != evaluator.Player)
                .Select(to => (From: from, To: to, Odds: BattleOdds.Estimate(board.Troops[from] - 1, board.Troops[to]).WinChance)))
            .ToArray();
        if (options.Length == 0) return null;
        // Eliminate a rival whose last territory is in reach; its cards come with it.
        var elimination = options.Where(a => a.Odds >= HomeOdds && board.Owners.Count(owner => owner == board.Owners[a.To]) == 1).OrderByDescending(a => a.Odds).FirstOrDefault();
        if (elimination.Odds > 0) return Command(elimination);
        var completion = options.Where(a => home.Contains(a.To) && a.Odds >= HomeOdds).OrderByDescending(a => a.Odds).FirstOrDefault();
        if (completion.Odds > 0) return Command(completion);
        if (board.Conquered) return null;
        // One cheap capture for the turn's card: the weakest defender, preferring targets that face few enemies afterwards.
        var farm = options.Where(a => a.Odds >= FarmOdds).OrderBy(a => board.Troops[a.To])
            .ThenBy(a => observation.Map.Territories[a.To].Neighbors.Count(n => board.Owners[n] >= 0 && board.Owners[n] != evaluator.Player)).FirstOrDefault();
        return farm.Odds > 0 ? Command(farm) : null;
    }

    private static GameCommand Command((int From, int To, double Odds) attack) => new() { Kind = CommandKind.Attack, From = attack.From, To = attack.To };

    /// <summary>Pull interior and surplus armies back to the fortress, leaving two on other borders.</summary>
    private static GameCommand Fortify(PositionEvaluator evaluator, PlannerBoard board, int[] home)
    {
        var fortress = Fortress(evaluator, board, home);
        var moves = Enumerable.Range(0, board.Owners.Length)
            .Where(from => from != fortress && board.Owners[from] == evaluator.Player && StrategicMoves.Connected(evaluator.Observation, from, fortress))
            .Select(from => (From: from, Count: board.Troops[from] - (evaluator.Border(board, from) ? 2 : 1)))
            .Where(move => move.Count >= 1).OrderByDescending(move => move.Count).ToArray();
        if (moves.Length == 0) return new() { Kind = CommandKind.EndTurn };
        return new() { Kind = CommandKind.Fortify, From = moves[0].From, To = fortress, Count = moves[0].Count };
    }
}
