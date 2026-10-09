using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI.Planning;

public sealed class PositionEvaluator
{
    private const double VictoryMargin = 1000;
    private const double VictoryArmyMultiplier = 10;
    private const double VictoryCardMultiplier = 100;
    private const double ThreatIncomeWeight = 3;
    private const double ThreatWeightMinimum = .25;
    private const double ThreatWeightMaximum = 3;
    public GameObservation Observation { get; }
    public ExpertTuning Tuning { get; }
    public int Player => Observation.Player;
    public int[][] Regions { get; }
    private readonly ObservedPlayer[] players;
    private readonly int[] regionIndex;
    private readonly double victoryValue;
    // Per-player facts fixed for the decision, indexed by player id; evaluation runs thousands of times per decision.
    private readonly bool[] eliminated;
    private readonly int[] tradeForecast;
    public double CardValue { get; }
    public bool Duel { get; }

    public PositionEvaluator(GameObservation observation, ExpertTuning tuning = null)
    {
        Observation = observation;
        Tuning = tuning ?? ExpertTuning.Default;
        players = observation.Players ?? observation.Territories.Where(t => t.Owner >= 0).Select(t => t.Owner).Distinct().Select(id => new ObservedPlayer(id, false, 0)).ToArray();
        Regions = observation.Map.Continents.Select(c => observation.Map.Territories.Where(t => t.Continent == c.Id).Select(t => t.Id).ToArray()).ToArray();
        regionIndex = observation.Map.Territories.Select(t => Array.FindIndex(observation.Map.Continents, c => c.Id == t.Continent)).ToArray();
        CardValue = ((observation.Options?.Cards ?? CardMode.Fixed) == CardMode.Fixed ? Tuning.FixedSetValue : CardRules.ProgressiveBonus(observation.Trades)) / 3.0;
        victoryValue = VictoryMargin + (observation.Territories.Sum(t => (double)t.Troops) + Math.Max(0, observation.Reinforcements)) * VictoryArmyMultiplier + CardValue * VictoryCardMultiplier;
        Duel = players.Count(p => !p.Eliminated) == 2;
        var count = Math.Max(Player + 1, players.Select(p => p.Id + 1).DefaultIfEmpty(0).Max());
        eliminated = new bool[count];
        tradeForecast = new int[count];
        foreach (var player in players)
        {
            eliminated[player.Id] = player.Eliminated;
            var chance = player.CardCount switch { >= 5 => 1.0, 4 => .75, 3 => .35, _ => 0.0 };
            tradeForecast[player.Id] = (int)Math.Round(chance * CardValue * 3);
        }
    }

    public double Evaluate(PlannerBoard board)
    {
        var score = Score(board, out var incomes);
        if (incomes == null) return score;
        // The learned correction reads the hand-written score it was trained on, before any frontier risk.
        if (Tuning.Valuation != null) score += Tuning.Valuation.Correction(this, board, score);
        if (FrontierWeight > 0) score -= FrontierWeight * FrontierRisk(board, incomes);
        return score;
    }

    /// <summary>The hand-written score alone, without the learned correction or frontier risk; value models calibrate against it.</summary>
    public double HandScore(PlannerBoard board) => Score(board, out _);

    /// <summary>The hand-written score; <paramref name="incomes"/> is null when the board is already won.</summary>
    private double Score(PlannerBoard board, out int[] incomes)
    {
        incomes = null;
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
        incomes = new int[count];
        incomes[Player] = Income(board, Player, territories[Player]);
        var score = armies[Player] + territories[Player] * Tuning.TerritoryValue + incomes[Player] * Tuning.IncomeValue;
        var rivals = players.Where(p => p.Id != Player && !p.Eliminated).ToArray();
        foreach (var rival in rivals) incomes[rival.Id] = Income(board, rival.Id, territories[rival.Id]);
        var weights = ThreatWeights(rivals, armies, incomes);
        foreach (var opponent in rivals)
        {
            var loss = armies[opponent.Id] * (Duel ? Tuning.DuelEnemyArmyValue : Tuning.MultiplayerEnemyArmyValue)
                + incomes[opponent.Id] * (Duel ? Tuning.DuelEnemyIncomeValue : Tuning.MultiplayerEnemyIncomeValue);
            score -= weights == null ? loss : loss * weights[opponent.Id];
        }
        score += EliminationValue(board, territories) + (board.Conquered ? CardValue : 0);
        // Exposure stays a separate subtraction so Expert and Master scores keep their exact floating-point values.
        score -= Exposure(board);
        return score;
    }

    /// <summary>The frontier-risk weight for the current number of players; zero when the term is off.</summary>
    public double FrontierWeight => Duel ? Tuning.DuelFrontierRiskValue : Tuning.FrontierRiskValue;

    /// <summary>Rival weights relative to the average rival by armies, income and cards; null when disabled or in a duel.</summary>
    private double[] ThreatWeights(ObservedPlayer[] rivals, double[] armies, int[] incomes)
    {
        if (Tuning.ThreatWeighting <= 0 || Duel) return null;
        var strength = new double[incomes.Length];
        foreach (var rival in rivals) strength[rival.Id] = armies[rival.Id] + incomes[rival.Id] * ThreatIncomeWeight + rival.CardCount * CardValue;
        var mean = rivals.Average(r => strength[r.Id]);
        if (mean <= 0) return null;
        var weights = new double[incomes.Length];
        foreach (var rival in rivals) weights[rival.Id] = Math.Clamp(Math.Pow(strength[rival.Id] / mean, Tuning.ThreatWeighting), ThreatWeightMinimum, ThreatWeightMaximum);
        return weights;
    }

    /// <summary>
    /// Expected loss from borders the rivals can take next turn: for each own border territory, the strongest adjacent enemy
    /// stack's capture chance times the territory, its income share and its defenders; the best such chance also costs the
    /// card the capturer would draw.
    /// </summary>
    private double FrontierRisk(PlannerBoard board, int[] incomes)
    {
        var risk = 0.0;
        var worst = 0.0;
        for (var id = 0; id < board.Owners.Length; id++)
        {
            if (board.Owners[id] != Player) continue;
            var chance = 0.0;
            foreach (var n in Observation.Map.Territories[id].Neighbors)
            {
                var owner = board.Owners[n];
                // Surrendered armies stay on the board but never attack.
                if (owner < 0 || owner == Player || board.Troops[n] < 2 || Eliminated(owner)) continue;
                var attackers = board.Troops[n] - 1 + incomes[owner] / 2 + TradeForecast(owner);
                chance = Math.Max(chance, BattleOdds.Estimate(attackers, board.Troops[id]).WinChance);
            }
            if (chance == 0) continue;
            risk += chance * (Tuning.TerritoryValue + Tuning.IncomeValue / 3 + board.Troops[id]);
            worst = Math.Max(worst, chance);
        }
        return risk + worst * CardValue;
    }

    public int Income(PlannerBoard board, int player, int owned = -1)
    {
        if (Eliminated(player)) return 0;
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
        if (Eliminated(owner)) return 0;
        return Math.Max(0, board.Troops[id] - 1 + Income(board, owner) / 2 + TradeForecast(owner));
    }

    private bool Eliminated(int player) => player >= 0 && player < eliminated.Length && eliminated[player];
    private int TradeForecast(int player) => player >= 0 && player < tradeForecast.Length ? tradeForecast[player] : 0;

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
