using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>Classic-map duel board encoding from the planning player's perspective; the rival moves next.</summary>
public sealed class DuelBoardFeatures : BoardEncoding
{
    public const int Territories = 42;
    public const int Continents = 6;
    private const int Side = 7;
    private const int PerTerritory = Side * 2;
    private const int Globals = 21 + Continents * 2;
    private static readonly int[] Thresholds = [2, 3, 5, 9];

    public override string Version => "duel-board-v1";
    public override int Count => Territories * PerTerritory + Globals;
    public override int ScoreIndex => Territories * PerTerritory;

    public override bool Supports(PositionEvaluator evaluator) =>
        evaluator.Duel && evaluator.Observation.Players != null
        && evaluator.Observation.Map.Territories.Length == Territories && evaluator.Regions.Length == Continents;

    public override void Extract(PositionEvaluator evaluator, PlannerBoard board, double score, float[] x)
    {
        Array.Clear(x);
        var player = evaluator.Player;
        var observation = evaluator.Observation;
        var map = observation.Map;
        var rival = observation.Players.First(p => p.Id != player && !p.Eliminated).Id;
        int ownTerritories = 0, rivalTerritories = 0, ownArmies = 0, rivalArmies = 0;
        for (var id = 0; id < Territories; id++)
        {
            var owner = board.Owners[id];
            if (owner != player && owner != rival) continue;
            var troops = board.Troops[id];
            var at = id * PerTerritory + (owner == player ? 0 : Side);
            x[at] = 1;
            for (var t = 0; t < Thresholds.Length; t++) if (troops >= Thresholds[t]) x[at + 1 + t] = 1;
            x[at + 5] = Math.Min(troops, 40) / 40f;
            // Chance that the strongest adjacent enemy stack captures this territory outright.
            var threat = 0.0;
            foreach (var neighbor in map.Territories[id].Neighbors)
                if (board.Owners[neighbor] != owner && board.Owners[neighbor] >= 0 && board.Troops[neighbor] > 1)
                    threat = Math.Max(threat, BattleOdds.Estimate(board.Troops[neighbor] - 1, troops).WinChance);
            x[at + 6] = (float)threat;
            if (owner == player) { ownTerritories++; ownArmies += troops; } else { rivalTerritories++; rivalArmies += troops; }
        }
        var g = ScoreIndex;
        x[g++] = (float)(score / ScoreScale);
        x[g++] = evaluator.Income(board, player, ownTerritories) / 20f;
        x[g++] = evaluator.Income(board, rival, rivalTerritories) / 20f;
        x[g++] = ownTerritories / 42f;
        x[g++] = rivalTerritories / 42f;
        x[g++] = ownArmies / 100f;
        x[g++] = rivalArmies / 100f;
        var ownCards = observation.Cards.Length + (board.Conquered ? 1 : 0);
        var rivalCards = observation.Players.First(p => p.Id == rival).CardCount;
        x[g + Math.Min(ownCards, 5)] = 1; g += 6;
        x[g + Math.Min(rivalCards, 5)] = 1; g += 6;
        x[g++] = board.Conquered ? 1 : 0;
        for (var r = 0; r < Continents; r++)
        {
            var region = evaluator.Regions[r];
            var owned = 0; var held = 0;
            foreach (var id in region) { if (board.Owners[id] == player) owned++; else if (board.Owners[id] == rival) held++; }
            x[g + r] = owned == region.Length ? 1 : 0;
            x[g + Continents + r] = held == region.Length ? 1 : 0;
        }
        g += Continents * 2;
        x[g] = Math.Min(observation.Trades, 10) / 10f;
    }
}
