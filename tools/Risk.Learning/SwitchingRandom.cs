using Risk.Sim;
namespace Risk.Learning;

/// <summary>Draws from one seed until switched, then from another; lets one starting deal be replayed with different dice.</summary>
public sealed class SwitchingRandom(int seed) : IRandomSource
{
    private Random random = new(seed);
    public void Switch(int seed) => random = new Random(seed);
    public int Next(int exclusiveMaximum) => random.Next(exclusiveMaximum);
}
