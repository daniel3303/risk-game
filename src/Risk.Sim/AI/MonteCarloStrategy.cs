using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim.AI;

public sealed class MonteCarloStrategy(IRandomSource random) : IPlayerStrategy
{
    private const int Samples = 96;
    public string Id => "monte-carlo-battles";

    public GameCommand Choose(GameObservation observation)
    {
        var prepared = StrategicMoves.Prepare(observation);
        if (prepared != null) return prepared;
        var threshold = observation.ConqueredThisTurn ? 0.88 : 0.7;
        var attack = StrategicMoves.Attacks(observation).OrderByDescending(a => a.Score).Take(8)
            .Select(a => (Candidate: a, Chance: WinChance(a.From.Troops - 1, a.To.Troops)))
            .Where(a => a.Chance >= threshold).OrderByDescending(a => a.Chance + a.Candidate.Score * 0.1).FirstOrDefault();
        return attack.Candidate == null ? new() { Kind = CommandKind.EndAttack }
            : new() { Kind = CommandKind.Attack, From = attack.Candidate.From.Id, To = attack.Candidate.To.Id };
    }

    private double WinChance(int attackers, int defenders)
    {
        // Bounded samples use a separate RNG and never inspect the live dice stream.
        var scale = Math.Max(1.0, Math.Max(attackers, defenders) / 80.0);
        attackers = Math.Max(1, (int)(attackers / scale));
        defenders = Math.Max(1, (int)(defenders / scale));
        var wins = 0;
        for (var sample = 0; sample < Samples; sample++)
        {
            var a = attackers;
            var d = defenders;
            while (a > 0 && d > 0)
            {
                var attack = CombatRules.Roll(Math.Min(3, a), random);
                var defend = CombatRules.Roll(Math.Min(2, d), random);
                for (var i = 0; i < Math.Min(attack.Length, defend.Length); i++)
                    if (attack[i] > defend[i]) d--; else a--;
            }
            if (d == 0) wins++;
        }
        return wins / (double)Samples;
    }
}
