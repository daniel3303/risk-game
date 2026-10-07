namespace Risk.Learning;

/// <summary>Adam moment estimates and step count for every parameter of one network.</summary>
public sealed class AdamState(int parameters)
{
    public double[] M { get; } = new double[parameters];
    public double[] S { get; } = new double[parameters];
    public int Step { get; set; }
}
