using Risk.Sim.AI.Probes;
namespace Risk.Sim.AI;

/// <summary>Every policy the Arena and learning tools can seat: the lobby difficulties plus benchmark probes that imitate human styles.</summary>
public static class PolicyCatalog
{
    /// <summary>Probes are evaluation opponents only; the lobby never offers them.</summary>
    public static IReadOnlyList<string> Probes { get; } = ["turtle"];

    /// <summary>Presets for other rule sets than the lobby's; like probes, the lobby never offers them.</summary>
    public static IReadOnlyList<string> Presets { get; } = ["smg"];

    public static IEnumerable<string> Names => Enum.GetNames<BotDifficulty>().Select(name => name.ToLowerInvariant()).Concat(Presets).Concat(Probes);

    public static bool IsKnown(string name) => name != null && Names.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static IPlayerStrategy Create(string name, int seed)
    {
        var difficulty = Enum.GetNames<BotDifficulty>().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (difficulty != null) return StrategyCatalog.Create(Enum.Parse<BotDifficulty>(difficulty), seed);
        return name?.ToLowerInvariant() switch
        {
            "turtle" => new TurtleStrategy(),
            "smg" => new SmgStrategy(),
            _ => throw new ArgumentException($"Unknown policy {name}."),
        };
    }
}
