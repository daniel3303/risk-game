namespace Risk.Sim.Models;

public sealed record GameOptions(CardMode Cards = CardMode.Fixed, SetupMode Setup = SetupMode.Automatic);
