namespace Risk.Learning;

public static class ValueTargets
{
    /// <summary>
    /// TD(λ) returns for one player's afterstates in turn order: the last takes the game outcome,
    /// each earlier one blends the bootstrap value of the next afterstate with that next afterstate's return.
    /// </summary>
    public static double[] LambdaReturns(IReadOnlyList<double> values, double outcome, double lambda)
    {
        var returns = new double[values.Count];
        if (returns.Length == 0) return returns;
        var next = outcome;
        returns[^1] = next;
        for (var i = returns.Length - 2; i >= 0; i--)
        {
            next = (1 - lambda) * values[i + 1] + lambda * next;
            returns[i] = next;
        }
        return returns;
    }
}
