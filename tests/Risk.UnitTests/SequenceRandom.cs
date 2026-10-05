using Risk.Sim;
namespace Risk.UnitTests;

internal sealed class SequenceRandom(int[] values) : IRandomSource
{
    private int index;
    public int Next(int exclusiveMaximum) => values[index++ % values.Length] % exclusiveMaximum;
}
