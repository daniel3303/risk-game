using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
namespace Risk.Sim.Learning;

/// <summary>
/// The forecast encoding plus what each rival is building toward. Per territory: an own territory that is the last one keeping a
/// rival from a whole continent, and a rival territory inside a continent its owner holds whole, both scaled by the bonus. Per rival
/// slot and continent: the share the slot holds and its chance of holding the whole continent after its next turn. Per continent:
/// the best forecast chance of a rival taking a territory of an own complete continent. Then each slot's expected continent bonus
/// and best chance of a new continent. Slots follow seat order: the next mover first, the remaining rivals together by their maximum.
/// </summary>
public sealed class IntentBoardFeatures : BoardEncoding
{
    private const int Territories = MultiplayerBoardFeatures.Territories;
    private const int Continents = MultiplayerBoardFeatures.Continents;
    private const int PerTerritory = 2;
    private const int Slots = 2;
    private const int PerContinent = Slots * 2 + 1;
    private const int Globals = Slots * 2;
    /// <summary>A rival missing more territories than this is not expected to complete the continent in one turn.</summary>
    private const int MaxMissing = 4;
    private const float MaxBonus = 7;
    private static BoardEncoding Basic => MultiplayerForecast;

    public override string Version => "multi-board-v3";
    public override int Count => Basic.Count + Territories * PerTerritory + Continents * PerContinent + Globals;
    public override int ScoreIndex => Basic.ScoreIndex;
    public override bool Supports(PositionEvaluator evaluator) => Basic.Supports(evaluator);

    public override void Extract(PositionEvaluator evaluator, PlannerBoard board, double score, float[] x)
    {
        Basic.Extract(evaluator, board, score, x);
        var player = evaluator.Player;
        var observation = evaluator.Observation;
        var map = observation.Map;
        var rivals = ForecastBoardFeatures.RivalsInSeatOrder(observation, player).ToArray();
        var attackers = new int[Territories];
        for (var id = 0; id < Territories; id++) if (board.Owners[id] >= 0) attackers[id] = evaluator.ForecastAttackers(board, id);
        var at = Basic.Count;
        var continentAt = at + Territories * PerTerritory;
        var globals = continentAt + Continents * PerContinent;
        for (var r = 0; r < Continents; r++)
        {
            var region = evaluator.Regions[r];
            var bonus = map.Continents[r].Bonus;
            var c = continentAt + r * PerContinent;
            for (var i = 0; i < rivals.Length; i++)
            {
                var rival = rivals[i];
                var slot = Math.Min(i, Slots - 1);
                var held = region.Count(id => board.Owners[id] == rival);
                var chance = CompletionChance(evaluator, board, attackers, region, rival, held);
                x[c + slot * 2] = Math.Max(x[c + slot * 2], held / (float)region.Length);
                x[c + slot * 2 + 1] = Math.Max(x[c + slot * 2 + 1], (float)chance);
                if (held == region.Length) foreach (var id in region) x[at + id * PerTerritory + 1] = bonus / MaxBonus;
                else if (held == region.Length - 1)
                {
                    var last = region.First(id => board.Owners[id] != rival);
                    if (board.Owners[last] == player) x[at + last * PerTerritory] = bonus / MaxBonus;
                }
            }
            if (region.All(id => board.Owners[id] == player))
                x[c + Slots * 2] = region.Max(id => x[Multiplayer.Count + id * ForecastBoardFeatures.PerTerritory + ForecastBoardFeatures.ThreatOffset]);
        }
        for (var slot = 0; slot < Slots; slot++)
        {
            double expected = 0, best = 0;
            for (var r = 0; r < Continents; r++)
            {
                var c = continentAt + r * PerContinent + slot * 2;
                expected += x[c + 1] * map.Continents[r].Bonus;
                if (x[c] < 1) best = Math.Max(best, x[c + 1]);
            }
            x[globals + slot * 2] = (float)(expected / 10);
            x[globals + slot * 2 + 1] = (float)best;
        }
    }

    /// <summary>
    /// The chance <paramref name="rival"/> holds the whole region after its next turn: the product, over the territories it lacks,
    /// of its best forecast capture from an adjacent stack it owns. Capture chains and spent armies are ignored.
    /// </summary>
    private static double CompletionChance(PositionEvaluator evaluator, PlannerBoard board, int[] attackers, int[] region, int rival, int held)
    {
        if (held == region.Length) return 1;
        if (region.Length - held > MaxMissing) return 0;
        var chance = 1.0;
        foreach (var id in region)
        {
            if (board.Owners[id] == rival) continue;
            var best = 0.0;
            foreach (var n in evaluator.Observation.Map.Territories[id].Neighbors)
                if (board.Owners[n] == rival) best = Math.Max(best, evaluator.Odds(attackers[n], board.Troops[id]).WinChance);
            chance *= best;
            if (chance == 0) break;
        }
        return chance;
    }
}
