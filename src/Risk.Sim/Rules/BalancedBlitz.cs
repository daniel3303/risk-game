namespace Risk.Sim.Rules;

/// <summary>
/// The mobile game's balanced blitz dice (<c>docs/balanced-blitz.md</c>). No dice are rolled: the exact blitz distribution
/// is skewed towards the favourite and one all-in outcome is sampled from a single uniform.
/// </summary>
public static class BalancedBlitz
{
    // The rule's single-precision constants, widened to double.
    private const double NeverWinsAtOrBelow = 0.05f;
    private const double FavouritePower = 1.3f;
    private const double TailTrim = 0.1f;
    private const double OutcomeSharpening = 1.8f;

    /// <summary>The balanced distribution, laid out like <see cref="BlitzCalculator.Calculate"/>.</summary>
    public static (double[] AttackLoss, double[] DefendLoss) Odds(int attack, int defend)
    {
        var (attackLoss, defendLoss) = BlitzCalculator.Calculate(attack, defend);
        Balance(attackLoss, defendLoss);
        return (attackLoss, defendLoss);
    }

    /// <summary>Battles with this many units in total (attack + defend) are estimated by the mobile game; this rule leaves them to real dice.</summary>
    public const int EstimationTotal = 2001;

    /// <summary>
    /// The losses of an all-in blitz of <paramref name="attack"/> units against <paramref name="defend"/>, picked by
    /// <paramref name="uniform"/> in [0, 1): the attacker's win chance comes first, then the defender's outcomes.
    /// </summary>
    public static (int AttackerLosses, int DefenderLosses) Resolve(int attack, int defend, double uniform)
    {
        if (double.IsNaN(uniform)) throw new ArgumentOutOfRangeException(nameof(uniform), "uniform must be in [0, 1).");
        var (attackLoss, defendLoss) = Odds(attack, defend);
        var u = Math.Clamp(uniform, double.Epsilon, BitConverter.Int64BitsToDouble(0x3FEFFFFFFFFFFFFF));
        if (defendLoss[defend] >= u) return (Pick(attackLoss, attack, u), defend);
        return (attack, Pick(defendLoss, defend, u - defendLoss[defend]));
    }

    /// <summary>A uniform double in [0, 1) with 53 random bits, as the rule draws one per battle.</summary>
    public static double NextUniform(IRandomSource random) =>
        (random.Next(1 << 26) * (double)(1 << 27) + random.Next(1 << 27)) / (1L << 53);

    /// <summary>The four balancing steps, in order.</summary>
    internal static void Balance(double[] attackLoss, double[] defendLoss)
    {
        var attack = attackLoss.Length - 1;
        var defend = defendLoss.Length - 1;

        // Win chance cutoff: a side below 5% never wins.
        if (NeverWinsAtOrBelow >= defendLoss[defend] && !(NeverWinsAtOrBelow >= attackLoss[attack]))
        {
            Array.Clear(attackLoss, 0, attack);
            attackLoss[attack] = 1;
            defendLoss[defend] = 0;
            ScaleTo(defendLoss, defend, 1);
        }
        else if (!(NeverWinsAtOrBelow >= defendLoss[defend]) && NeverWinsAtOrBelow >= attackLoss[attack])
        {
            Array.Clear(defendLoss, 0, defend);
            defendLoss[defend] = 1;
            attackLoss[attack] = 0;
            ScaleTo(attackLoss, attack, 1);
        }

        // Win chance power: push the win chance towards the favourite.
        var win = Math.Pow(defendLoss[defend], FavouritePower);
        var loss = Math.Pow(attackLoss[attack], FavouritePower);
        var scale = 1 / (loss + win);
        win *= scale;
        loss *= scale;
        ScaleTo(attackLoss, attack, win);
        ScaleTo(defendLoss, defend, loss);
        attackLoss[attack] = loss;
        defendLoss[defend] = win;

        // Outcome cutoff: drop the 10% tails at both ends of the outcome line, best attacker outcome first.
        var line = new double[attack + defend];
        for (var i = 0; i < attack; i++) line[i] = attackLoss[i];
        for (var k = 0; k < defend; k++) line[attack + k] = defendLoss[defend - 1 - k];
        TrimTail(line, Enumerable.Range(0, line.Length));
        TrimTail(line, Enumerable.Range(0, line.Length).Reverse());
        for (var i = 0; i < attack; i++) attackLoss[i] = line[i];
        for (var k = 0; k < defend; k++) defendLoss[defend - 1 - k] = line[attack + k];
        var attackerWins = SumTo(attackLoss, attack);
        var defenderWins = SumTo(defendLoss, defend);
        scale = 1 / (attackerWins + defenderWins);
        win = attackerWins * scale;
        loss = defenderWins * scale;
        ScaleTo(attackLoss, attack, win);
        ScaleTo(defendLoss, defend, loss);
        attackLoss[attack] = loss;
        defendLoss[defend] = win;

        // Outcome power: sharpen the outcomes within each side, keeping the win chance.
        for (var i = 0; i < attack; i++) attackLoss[i] = Math.Pow(attackLoss[i], OutcomeSharpening);
        for (var j = 0; j < defend; j++) defendLoss[j] = Math.Pow(defendLoss[j], OutcomeSharpening);
        ScaleTo(attackLoss, attack, defendLoss[defend]);
        ScaleTo(defendLoss, defend, attackLoss[attack]);
    }

    private static void TrimTail(double[] line, IEnumerable<int> order)
    {
        var sum = 0.0;
        foreach (var i in order)
        {
            sum += line[i];
            if (sum > TailTrim)
            {
                line[i] = sum - TailTrim;
                return;
            }
            line[i] = 0;
        }
    }

    /// <summary>Scales the first <paramref name="count"/> probabilities to sum to <paramref name="target"/>, spreading it evenly when they are all zero.</summary>
    private static void ScaleTo(double[] values, int count, double target)
    {
        if (target <= 0)
        {
            Array.Clear(values, 0, count);
            return;
        }
        var sum = SumTo(values, count);
        for (var i = 0; i < count; i++) values[i] = sum <= 0 ? target / count : values[i] * (target / sum);
    }

    // Left to right, so the sums match the rule exactly: LINQ may reorder a floating-point sum.
    private static double SumTo(double[] values, int end, int start = 0)
    {
        var sum = 0.0;
        for (var i = start; i < end; i++) sum += values[i];
        return sum;
    }

    // The first positive entry whose running total reaches u. Rounding can leave u just past the total (about once in
    // 1e16 battles); the bucket u falls in is then the last positive one, which keeps the result a legal, finished blitz.
    private static int Pick(double[] losses, int count, double u)
    {
        var sum = 0.0;
        var last = 0;
        for (var i = 0; i < count; i++)
        {
            if (losses[i] <= 0) continue;
            last = i;
            sum += losses[i];
            if (sum >= u) return i;
        }
        return last;
    }
}
