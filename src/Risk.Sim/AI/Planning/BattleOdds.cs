namespace Risk.Sim.AI.Planning;

public static class BattleOdds
{
    public const int ExactLimit = 512;
    private static readonly DiceOutcome[][][] rounds = CreateRounds();
    private static readonly Lazy<BattleEstimate[,]> table = new(CreateTable);

    public static BattleEstimate Estimate(int attackers, int defenders)
    {
        if (attackers < 0 || defenders < 0) throw new ArgumentOutOfRangeException(nameof(attackers));
        if (defenders == 0) return new(1, attackers, 0);
        if (attackers == 0) return new(0, 0, defenders);
        return attackers <= ExactLimit && defenders <= ExactLimit ? table.Value[attackers, defenders] : LargeBattle(attackers, defenders);
    }

    public static DiceOutcome[] Round(int attackDice, int defendDice)
    {
        if (attackDice is < 1 or > 3 || defendDice is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(attackDice));
        return rounds[attackDice][defendDice].ToArray();
    }

    private static BattleEstimate[,] CreateTable()
    {
        var result = new BattleEstimate[ExactLimit + 1, ExactLimit + 1];
        for (var a = 0; a <= ExactLimit; a++) result[a, 0] = new(1, a, 0);
        for (var d = 1; d <= ExactLimit; d++) result[0, d] = new(0, 0, d);
        for (var a = 1; a <= ExactLimit; a++)
            for (var d = 1; d <= ExactLimit; d++) result[a, d] = Combine(result, a, d);
        return result;
    }

    private static BattleEstimate Combine(BattleEstimate[,] result, int a, int d)
    {
        double chance = 0, armies = 0, defenders = 0;
        foreach (var outcome in rounds[Math.Min(3, a)][Math.Min(2, d)])
        {
            var next = result[a - outcome.AttackerLosses, d - outcome.DefenderLosses];
            chance += outcome.Probability * next.WinChance;
            armies += outcome.Probability * next.WinningArmies;
            defenders += outcome.Probability * next.LosingDefenders;
        }
        return new(Math.Clamp(chance, 0, 1), armies, defenders);
    }

    private static DiceOutcome[][][] CreateRounds()
    {
        var result = new DiceOutcome[4][][];
        for (var a = 1; a <= 3; a++)
        {
            result[a] = new DiceOutcome[3][];
            for (var d = 1; d <= 2; d++) result[a][d] = Enumerate(a, d);
        }
        return result;
    }

    private static DiceOutcome[] Enumerate(int attackers, int defenders)
    {
        var total = (int)Math.Pow(6, attackers + defenders);
        var counts = new Dictionary<(int A, int D), int>();
        for (var roll = 0; roll < total; roll++)
        {
            var encoded = roll;
            var attack = Decode(attackers, ref encoded);
            var defend = Decode(defenders, ref encoded);
            var lost = Enumerable.Range(0, Math.Min(attackers, defenders)).Count(i => attack[i] <= defend[i]);
            var key = (lost, Math.Min(attackers, defenders) - lost);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts.Select(c => new DiceOutcome(c.Key.A, c.Key.D, c.Value / (double)total)).ToArray();
    }

    private static int[] Decode(int count, ref int value)
    {
        var dice = new int[count];
        for (var i = 0; i < count; i++) { dice[i] = value % 6; value /= 6; }
        Array.Sort(dice); Array.Reverse(dice);
        return dice;
    }

    private static BattleEstimate LargeBattle(int attackers, int defenders)
    {
        // A renewal approximation keeps progressive-card armies from growing a quadratic table.
        var distribution = rounds[3][2];
        var meanDefenseLoss = distribution.Sum(r => r.Probability * r.DefenderLosses);
        var ratio = (2 - meanDefenseLoss) / meanDefenseLoss;
        var variance = distribution.Sum(r => r.Probability * Math.Pow(r.AttackerLosses - ratio * r.DefenderLosses, 2)) / meanDefenseLoss;
        var sigma = Math.Sqrt(variance * defenders);
        var margin = attackers - ratio * defenders;
        var z = margin / sigma;
        var chance = NormalCdf(z);
        var density = Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI);
        var winning = chance > 0 ? Math.Clamp(margin + sigma * density / chance, 1, attackers) : 0;
        var losing = chance < 1 ? Math.Clamp(sigma / ratio * (density / (1 - chance) - z), 1, defenders) : 0;
        return new(chance, chance * winning, (1 - chance) * losing);
    }

    private static double NormalCdf(double z)
    {
        var t = 1 / (1 + .2316419 * Math.Abs(z));
        var polynomial = t * (.319381530 + t * (-.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
        var tail = Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI) * polynomial;
        return Math.Clamp(z >= 0 ? 1 - tail : tail, 0, 1);
    }
}
