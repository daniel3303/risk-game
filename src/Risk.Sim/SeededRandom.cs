namespace Risk.Sim;

public sealed class SeededRandom(int seed) : IRandomSource
{
    private readonly Random random = new(seed);
    public int Next(int exclusiveMaximum) => random.Next(exclusiveMaximum);
}
