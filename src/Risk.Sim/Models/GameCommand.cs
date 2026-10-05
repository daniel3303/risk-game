namespace Risk.Sim.Models;

public sealed record GameCommand
{
    public CommandKind Kind { get; init; }
    public int From { get; init; } = -1;
    public int To { get; init; } = -1;
    public int Count { get; init; }
    public int Dice { get; init; } = 3;
    public bool Blitz { get; init; } = true;
    public int[] Cards { get; init; } = [];
    public int BonusTerritory { get; init; } = -1;
}
