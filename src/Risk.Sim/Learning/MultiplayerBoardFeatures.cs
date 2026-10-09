using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>
/// Classic-map board encoding for games with more than two players, from the planning player's perspective. Besides ownership,
/// armies and threats it exposes the shape of a position (own borders, interior, continent gates and entry counts) and how close
/// the weakest rival is to elimination, so that a learned value can prefer short frontiers, continents with few entrances, and
/// finishing a player for its cards.
/// </summary>
public sealed class MultiplayerBoardFeatures : BoardEncoding
{
    public const int Territories = 42;
    public const int Continents = 6;
    private const int PerTerritory = 12;
    private const int MaxPlayers = 6;
    private const int Globals = 1 + 3 + 3 + 3 + 6 + 6 + 1 + 1 + Continents * 4 + 4 + (MaxPlayers - 1) + 1 + Elimination;
    /// <summary>The weakest rival's territories, armies, cards, and the worst and mean chance of capturing its territories outright from adjacent own stacks.</summary>
    private const int Elimination = 2 + 6 + 2;
    private static readonly int[] Thresholds = [2, 3, 5, 9];
    private const double ThreatIncomeWeight = 3;

    public override string Version => "multi-board-v1";
    public override int Count => Territories * PerTerritory + Globals;
    public override int ScoreIndex => Territories * PerTerritory;

    public override bool Supports(PositionEvaluator evaluator) =>
        !evaluator.Duel && evaluator.Observation.Players != null && evaluator.Observation.Players.Count(p => !p.Eliminated) > 2
        && evaluator.Observation.Map.Territories.Length == Territories && evaluator.Regions.Length == Continents;

    public override void Extract(PositionEvaluator evaluator, PlannerBoard board, double score, float[] x)
    {
        Array.Clear(x);
        var player = evaluator.Player;
        var observation = evaluator.Observation;
        var map = observation.Map;
        var rivals = observation.Players.Where(p => p.Id != player && !p.Eliminated).ToArray();
        var count = Math.Max(player + 1, observation.Players.Max(p => p.Id) + 1);
        var territories = new int[count];
        var armies = new int[count];
        for (var id = 0; id < Territories; id++)
        {
            var owner = board.Owners[id];
            if (owner < 0) continue;
            territories[owner]++;
            armies[owner] += board.Troops[id];
        }
        var incomes = new int[count];
        foreach (var p in observation.Players) incomes[p.Id] = evaluator.Income(board, p.Id, territories[p.Id]);
        // The strongest rival by armies, income and cards is the one whose territories and continents are encoded separately.
        var strongest = rivals.OrderByDescending(r => armies[r.Id] + incomes[r.Id] * ThreatIncomeWeight + r.CardCount * evaluator.CardValue).ThenBy(r => r.Id).First().Id;
        var complete = new bool[Continents];
        for (var r = 0; r < Continents; r++) complete[r] = evaluator.Regions[r].All(id => board.Owners[id] == player);
        int borders = 0, interior = 0, largest = 0, borderArmies = 0;
        for (var id = 0; id < Territories; id++)
        {
            var owner = board.Owners[id];
            if (owner < 0) continue;
            var troops = board.Troops[id];
            var at = id * PerTerritory;
            x[at + (owner == player ? 0 : owner == strongest ? 1 : 2)] = 1;
            for (var t = 0; t < Thresholds.Length; t++) if (troops >= Thresholds[t]) x[at + 3 + t] = 1;
            x[at + 7] = Math.Min(troops, 40) / 40f;
            var threat = 0.0;
            var enemyNeighbor = false;
            var outside = 0;
            foreach (var neighbor in map.Territories[id].Neighbors)
            {
                if (map.Territories[neighbor].Continent != map.Territories[id].Continent) outside++;
                var other = board.Owners[neighbor];
                if (other == owner) continue;
                if (other >= 0) enemyNeighbor = true;
                if (other >= 0 && board.Troops[neighbor] > 1) threat = Math.Max(threat, BattleOdds.Estimate(board.Troops[neighbor] - 1, troops).WinChance);
            }
            x[at + 8] = (float)threat;
            x[at + 11] = outside / 6f;
            if (owner != player) continue;
            largest = Math.Max(largest, troops);
            if (enemyNeighbor) { x[at + 9] = 1; borders++; borderArmies += troops; } else interior++;
            if (complete[RegionOf(evaluator, id)] && outside > 0) x[at + 10] = 1;
        }
        var g = ScoreIndex;
        x[g++] = (float)(score / ScoreScale);
        x[g++] = incomes[player] / 20f;
        x[g++] = incomes[strongest] / 20f;
        x[g++] = rivals.Sum(r => incomes[r.Id]) / 40f;
        x[g++] = territories[player] / 42f;
        x[g++] = territories[strongest] / 42f;
        x[g++] = rivals.Sum(r => territories[r.Id]) / 42f;
        x[g++] = armies[player] / 100f;
        x[g++] = armies[strongest] / 100f;
        x[g++] = rivals.Sum(r => armies[r.Id]) / 200f;
        var ownCards = observation.Cards.Length + (board.Conquered ? 1 : 0);
        x[g + Math.Min(ownCards, 5)] = 1; g += 6;
        x[g + Math.Min(rivals.Max(r => r.CardCount), 5)] = 1; g += 6;
        x[g++] = board.Conquered ? 1 : 0;
        x[g++] = rivals.Any(r => r.CardCount >= 4) ? 1 : 0;
        for (var r = 0; r < Continents; r++)
        {
            var region = evaluator.Regions[r];
            int mine = 0, theirs = 0, anyComplete = 0;
            foreach (var id in region) { if (board.Owners[id] == player) mine++; else if (board.Owners[id] == strongest) theirs++; }
            foreach (var rival in rivals) if (region.All(id => board.Owners[id] == rival.Id)) anyComplete = 1;
            x[g + r] = mine / (float)region.Length;
            x[g + Continents + r] = theirs / (float)region.Length;
            x[g + 2 * Continents + r] = complete[r] ? 1 : 0;
            x[g + 3 * Continents + r] = anyComplete;
        }
        g += Continents * 4;
        x[g++] = borders / 42f;
        x[g++] = interior / 42f;
        x[g++] = Math.Min(largest, 100) / 100f;
        x[g++] = armies[player] > 0 ? borderArmies / (float)armies[player] : 0;
        x[g + Math.Clamp(rivals.Length, 1, MaxPlayers - 1) - 1] = 1; g += MaxPlayers - 1;
        x[g++] = Math.Min(observation.Trades, 10) / 10f;
        var weakest = rivals.OrderBy(r => territories[r.Id]).ThenBy(r => armies[r.Id]).ThenBy(r => r.Id).First();
        x[g++] = territories[weakest.Id] / 42f;
        x[g++] = Math.Min(armies[weakest.Id], 100) / 100f;
        x[g + Math.Min(weakest.CardCount, 5)] = 1; g += 6;
        double worst = 1, total = 0;
        for (var id = 0; id < Territories; id++)
        {
            if (board.Owners[id] != weakest.Id) continue;
            var chance = 0.0;
            foreach (var neighbor in map.Territories[id].Neighbors)
                if (board.Owners[neighbor] == player && board.Troops[neighbor] > 1)
                    chance = Math.Max(chance, BattleOdds.Estimate(board.Troops[neighbor] - 1, board.Troops[id]).WinChance);
            worst = Math.Min(worst, chance);
            total += chance;
        }
        x[g++] = territories[weakest.Id] > 0 ? (float)worst : 0;
        x[g] = territories[weakest.Id] > 0 ? (float)(total / territories[weakest.Id]) : 0;
    }

    private static int RegionOf(PositionEvaluator evaluator, int id)
    {
        for (var r = 0; r < evaluator.Regions.Length; r++) if (Array.IndexOf(evaluator.Regions[r], id) >= 0) return r;
        return 0;
    }
}
