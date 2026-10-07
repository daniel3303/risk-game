using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI.Planning;

public sealed class PositionEvaluator
{
    private const double VictoryMargin = 1000;
    private const double VictoryArmyMultiplier = 10;
    private const double VictoryCardMultiplier = 100;
    public GameObservation Observation { get; }
    public ExpertTuning Tuning { get; }
    public int Player => Observation.Player;
    public int[][] Regions { get; }
    private readonly ObservedPlayer[] players;
    private readonly int[] regionIndex;
    private readonly double victoryValue;
    public double CardValue { get; }
    public bool Duel => players.Count(p => !p.Eliminated) == 2;

    public PositionEvaluator(GameObservation observation, ExpertTuning tuning = null)
    {
        Observation = observation;
        Tuning = tuning ?? ExpertTuning.Default;
        players = observation.Players ?? observation.Territories.Where(t => t.Owner >= 0).Select(t => t.Owner).Distinct().Select(id => new ObservedPlayer(id, false, 0)).ToArray();
        Regions = observation.Map.Continents.Select(c => observation.Map.Territories.Where(t => t.Continent == c.Id).Select(t => t.Id).ToArray()).ToArray();
        regionIndex = observation.Map.Territories.Select(t => Array.FindIndex(observation.Map.Continents, c => c.Id == t.Continent)).ToArray();
        CardValue = ((observation.Options?.Cards ?? CardMode.Fixed) == CardMode.Fixed ? Tuning.FixedSetValue : CardRules.ProgressiveBonus(observation.Trades)) / 3.0;
        victoryValue = VictoryMargin + (observation.Territories.Sum(t => (double)t.Troops) + Math.Max(0, observation.Reinforcements)) * VictoryArmyMultiplier + CardValue * VictoryCardMultiplier;
    }

    public double Evaluate(PlannerBoard board)
    {
        var count = Math.Max(Player + 1, players.Select(p => p.Id + 1).DefaultIfEmpty(0).Max());
        var territories = new int[count];
        var armies = new double[count];
        for (var id = 0; id < board.Owners.Length; id++)
        {
            if (board.Owners[id] < 0) continue;
            territories[board.Owners[id]]++;
            armies[board.Owners[id]] += board.Troops[id];
        }
        if (players.Where(p => p.Id != Player && !p.Eliminated).All(p => territories[p.Id] == 0)) return victoryValue;
        var ownIncome = Income(board, Player, territories[Player]);
        var score = armies[Player] + territories[Player] * Tuning.TerritoryValue + ownIncome * Tuning.IncomeValue;
        foreach (var opponent in players.Where(p => p.Id != Player && !p.Eliminated))
            score -= armies[opponent.Id] * (Duel ? Tuning.DuelEnemyArmyValue : Tuning.MultiplayerEnemyArmyValue)
                + Income(board, opponent.Id, territories[opponent.Id]) * (Duel ? Tuning.DuelEnemyIncomeValue : Tuning.MultiplayerEnemyIncomeValue);
        score += EliminationValue(board, territories) + (board.Conquered ? CardValue : 0) - Exposure(board);
        return Tuning.Valuation == null ? score : score + Tuning.Valuation.Correction(this, board, score);
    }

    public int Income(PlannerBoard board, int player, int owned = -1)
    {
        if (players.FirstOrDefault(p => p.Id == player)?.Eliminated == true) return 0;
        if (owned < 0) owned = board.Owners.Count(owner => owner == player);
        if (owned == 0) return 0;
        return Math.Max(3, owned / 3) + Enumerable.Range(0, Regions.Length)
            .Where(r => Regions[r].All(id => board.Owners[id] == player)).Sum(r => Observation.Map.Continents[r].Bonus);
    }

    public double Priority(PlannerBoard board, int from, int to)
    {
        var region = Regions[regionIndex[to]];
        var owned = region.Count(id => board.Owners[id] == Player);
        var enemy = board.Owners[to];
        var finalTerritory = board.Owners.Count(owner => owner == enemy) == 1 && players.Any(p => p.Id == enemy && !p.Eliminated);
        var bonus = Observation.Map.Continents[regionIndex[to]].Bonus;
        return (board.Troops[from] - 1.0) / Math.Max(1, board.Troops[to]) + bonus * (owned + 1.0) / region.Length
            + (finalTerritory ? 10 + (players.FirstOrDefault(p => p.Id == enemy)?.CardCount ?? 0) * CardValue : 0);
    }

    public bool Border(PlannerBoard board, int id) => Observation.Map.Territories[id].Neighbors.Any(n => board.Owners[n] != Player);
    public bool CompleteRegion(PlannerBoard board, int id) => Regions[regionIndex[id]].All(n => board.Owners[n] == Player);

    public int Guard(PlannerBoard board, int id)
    {
        if (!CompleteRegion(board, id)) return 1;
        var threats = Observation.Map.Territories[id].Neighbors.Where(n => board.Owners[n] >= 0 && board.Owners[n] != Player).ToArray();
        if (threats.Length == 0) return 1;
        return Math.Max(2, threats.Max(n => ForecastAttackers(board, n) + 1));
    }

    public int ForecastAttackers(PlannerBoard board, int id)
    {
        var owner = board.Owners[id];
        var opponent = players.FirstOrDefault(p => p.Id == owner);
        if (opponent?.Eliminated == true) return 0;
        var chance = opponent?.CardCount switch { >= 5 => 1.0, 4 => .75, 3 => .35, _ => 0.0 };
        var trade = (int)Math.Round(chance * CardValue * 3);
        return Math.Max(0, board.Troops[id] - 1 + Income(board, owner) / 2 + trade);
    }

    private double EliminationValue(PlannerBoard board, int[] territories) => players
        .Where(p => p.Id != Player && !p.Eliminated && territories[p.Id] == 0 && Observation.Territories.Any(t => t.Owner == p.Id))
        .Sum(p => Tuning.EliminationValueBase + p.CardCount * CardValue * Tuning.InheritedCardMultiplier);

    private double Exposure(PlannerBoard board)
    {
        var risk = 0.0;
        foreach (var r in Enumerable.Range(0, Regions.Length).Where(r => Regions[r].All(id => board.Owners[id] == Player)))
        {
            var threat = Regions[r].SelectMany(id => Observation.Map.Territories[id].Neighbors.Where(n => board.Owners[n] >= 0 && board.Owners[n] != Player)
                .Select(n => BattleOdds.Estimate(ForecastAttackers(board, n), board.Troops[id]).WinChance)).DefaultIfEmpty(0).Max();
            risk += threat * Observation.Map.Continents[r].Bonus * Tuning.ContinentExposureValue;
        }
        return risk;
    }
}
