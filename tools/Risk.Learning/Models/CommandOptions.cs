using System.Globalization;
namespace Risk.Learning.Models;

/// <summary>Parsed "--name value" pairs with typed, validated lookups.</summary>
public sealed class CommandOptions
{
    private readonly Dictionary<string, string> values;
    private readonly HashSet<string> used = new(StringComparer.Ordinal);

    private CommandOptions(Dictionary<string, string> values) => this.values = values;

    public static CommandOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count % 2 != 0) throw new ArgumentException("Supply options as --name value pairs.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Expected an option name, found {args[i]}.");
            if (!values.TryAdd(args[i], args[i + 1])) throw new ArgumentException($"Duplicate option {args[i]}.");
        }
        return new(values);
    }

    public string Text(string name, string fallback = null)
    {
        used.Add(name);
        return values.TryGetValue(name, out var value) ? value : fallback ?? throw new ArgumentException($"Missing option {name}.");
    }

    public string OptionalText(string name)
    {
        used.Add(name);
        return values.GetValueOrDefault(name);
    }

    public int Integer(string name, int fallback, int minimum, int maximum)
    {
        var value = values.ContainsKey(name) ? int.Parse(Text(name), CultureInfo.InvariantCulture) : fallback;
        used.Add(name);
        return value >= minimum && value <= maximum ? value : throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
    }

    public double Number(string name, double fallback, double minimum, double maximum)
    {
        var value = values.ContainsKey(name) ? double.Parse(Text(name), CultureInfo.InvariantCulture) : fallback;
        used.Add(name);
        return value >= minimum && value <= maximum ? value : throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
    }

    /// <summary>Rejects options the command did not read, so typos fail loudly.</summary>
    public void RejectUnknown()
    {
        var unknown = values.Keys.Where(key => !used.Contains(key)).ToArray();
        if (unknown.Length > 0) throw new ArgumentException($"Unknown option {unknown[0]}.");
    }
}
