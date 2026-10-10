using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>
/// The multiplayer encoding plus what happens next: per territory, whether the next player to move owns it, the chance the
/// strongest adjacent enemy stack takes an own territory once its owner adds forecast reinforcements, and the same chance for
/// an own stack against an enemy territory; then the next mover's totals and the summed forecast threat and opportunity.
/// </summary>
public sealed class ForecastBoardFeatures : BoardEncoding
{
    private const int Territories = MultiplayerBoardFeatures.Territories;
    private const int PerTerritory = 3;
    private const int Globals = 8;
    private static BoardEncoding Basic => Multiplayer;

    public override string Version => "multi-board-v2";
    public override int Count => Basic.Count + Territories * PerTerritory + Globals;
    public override int ScoreIndex => Basic.ScoreIndex;
    public override bool Supports(PositionEvaluator evaluator) => Basic.Supports(evaluator);

    public override void Extract(PositionEvaluator evaluator, PlannerBoard board, double score, float[] x)
    {
        Basic.Extract(evaluator, board, score, x);
        var player = evaluator.Player;
        var observation = evaluator.Observation;
        var map = observation.Map;
        var next = NextMover(observation, player);
        int at = Basic.Count, nextTerritories = 0, nextArmies = 0;
        double threat = 0, worst = 0, opportunity = 0;
        for (var id = 0; id < Territories; id++, at += PerTerritory)
        {
            var owner = board.Owners[id];
            if (owner < 0) continue;
            if (owner == next) { x[at] = 1; nextTerritories++; nextArmies += board.Troops[id]; }
            var chance = 0.0;
            foreach (var n in map.Territories[id].Neighbors)
            {
                var other = board.Owners[n];
                // Own territories face enemy stacks; enemy territories face own stacks.
                if (other < 0 || other == owner || (owner != player && other != player)) continue;
                chance = Math.Max(chance, BattleOdds.Estimate(evaluator.ForecastAttackers(board, n), board.Troops[id]).WinChance);
            }
            if (owner == player) { x[at + 1] = (float)chance; threat += chance; worst = Math.Max(worst, chance); }
            else { x[at + 2] = (float)chance; opportunity += chance; }
        }
        var g = at;
        var players = observation.Players;
        var rival = players.FirstOrDefault(p => p.Id == next);
        x[g++] = next >= 0 ? evaluator.Income(board, next, nextTerritories) / 20f : 0;
        x[g++] = nextTerritories / 42f;
        x[g++] = Math.Min(nextArmies, 100) / 100f;
        x[g++] = Math.Min(rival?.CardCount ?? 0, 5) / 5f;
        x[g++] = Enumerable.Range(0, Territories).Any(id => board.Owners[id] == next && x[id * MultiplayerBoardFeatures.PerTerritory + MultiplayerBoardFeatures.StrongestRivalFlag] == 1) ? 1 : 0;
        x[g++] = (float)(threat / 10);
        x[g++] = (float)worst;
        x[g] = (float)(opportunity / 10);
    }

    /// <summary>The first player after the mover, in seat order, who is still in the game; -1 when none is.</summary>
    private static int NextMover(AI.GameObservation observation, int player)
    {
        var count = observation.Players.Length;
        for (var step = 1; step < count; step++)
        {
            var candidate = observation.Players[(Array.FindIndex(observation.Players, p => p.Id == player) + step) % count];
            if (!candidate.Eliminated) return candidate.Id;
        }
        return -1;
    }
}
