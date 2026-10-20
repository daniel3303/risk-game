namespace Risk.Sim.Rules;

/// <summary>
/// The exact all-in blitz distribution that balanced blitz starts from (<c>docs/balanced-blitz.md</c>). Six faces, up to three attacking and two defending dice, ties to the defender.
/// <para><c>AttackLoss[i &lt; A]</c> = P(attacker wins having lost <c>i</c>), <c>AttackLoss[A]</c> = P(defender wins);
/// <c>DefendLoss[j &lt; D]</c> = P(defender wins having lost <c>j</c>), <c>DefendLoss[D]</c> = P(attacker wins).</para>
/// </summary>
public static class BlitzCalculator
{
    private const int Faces = 6;
    private const int MaxAttackDice = 3;
    private const int MaxDefendDice = 2;

    // [attackDice - 1][defendDice - 1] -> (attacker losses, defender losses, probability), sorted by losses.
    private static readonly (int AttackLoss, int DefendLoss, double Chance)[][][] Rounds = BuildRounds();

    public static (double[] AttackLoss, double[] DefendLoss) Calculate(int attack, int defend)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attack, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(defend, 1);
        var chance = new double[attack + 1][];
        for (var a = 0; a <= attack; a++) chance[a] = new double[defend + 1];
        chance[attack][defend] = 1;
        // Walk the remaining-unit diagonals downwards so every state is complete before it spreads.
        for (var sum = attack + defend; sum >= 2; sum--)
        {
            for (var a = Math.Min(attack, sum - 1); a >= Math.Max(1, sum - defend); a--)
            {
                var d = sum - a;
                var p = chance[a][d];
                if (p == 0) continue;
                foreach (var (attackLoss, defendLoss, q) in Rounds[Math.Min(a, MaxAttackDice) - 1][Math.Min(d, MaxDefendDice) - 1])
                    chance[a - attackLoss][d - defendLoss] += q * p;
            }
        }
        var attackLosses = new double[attack + 1];
        var defendLosses = new double[defend + 1];
        for (var a = 0; a <= attack; a++)
        {
            for (var d = 0; d <= defend; d++)
            {
                if (a != 0 && d != 0) continue;
                attackLosses[attack - a] += chance[a][d];
                defendLosses[defend - d] += chance[a][d];
            }
        }
        return (attackLosses, defendLosses);
    }

    private static (int, int, double)[][][] BuildRounds()
    {
        var rounds = new (int, int, double)[MaxAttackDice][][];
        for (var a = 1; a <= MaxAttackDice; a++)
        {
            rounds[a - 1] = new (int, int, double)[MaxDefendDice][];
            for (var d = 1; d <= MaxDefendDice; d++) rounds[a - 1][d - 1] = EnumerateRound(a, d);
        }
        return rounds;
    }

    private static (int, int, double)[] EnumerateRound(int attackDice, int defendDice)
    {
        var dice = attackDice + defendDice;
        var total = (int)Math.Pow(Faces, dice);
        var counts = new SortedDictionary<(int, int), int>();
        var roll = new int[dice];
        for (var code = 0; code < total; code++)
        {
            for (int i = 0, rest = code; i < dice; i++, rest /= Faces) roll[i] = rest % Faces + 1;
            var attack = roll[..attackDice].OrderDescending().ToArray();
            var defend = roll[attackDice..].OrderDescending().ToArray();
            var (attackLoss, defendLoss) = (0, 0);
            for (var pair = 0; pair < Math.Min(attackDice, defendDice); pair++)
            {
                if (attack[pair] > defend[pair]) defendLoss++;
                else attackLoss++;
            }
            counts[(attackLoss, defendLoss)] = counts.GetValueOrDefault((attackLoss, defendLoss)) + 1;
        }
        return counts.Select(c => (c.Key.Item1, c.Key.Item2, c.Value / (double)total)).ToArray();
    }
}
