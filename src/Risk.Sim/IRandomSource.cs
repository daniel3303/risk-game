namespace Risk.Sim;

public interface IRandomSource
{
    int Next(int exclusiveMaximum);
}
