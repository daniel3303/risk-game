using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Arena.Models;

public sealed record ArenaOptions(int Seeds = 16, int FirstSeed = 1000, int Players = 2,
    BotDifficulty Candidate = BotDifficulty.Expert, BotDifficulty Opponent = BotDifficulty.Hard,
    CardMode Cards = CardMode.Fixed, SetupMode Setup = SetupMode.Automatic, int MaxRounds = 200, int MaxActions = 20000, int Parallelism = 1)
{
    public void Validate()
    {
        if (Seeds is < 1 or > 5000 || Players is < 2 or > 6 || MaxRounds is < 1 or > 1000 || MaxActions is < 1 or > 100000 || Parallelism is < 1 or > 64)
            throw new ArgumentException("Use 1–5000 seeds, 2–6 players, 1–1000 rounds, 1–100000 actions, and 1–64 parallel matches.");
        if (FirstSeed < 0 || FirstSeed > int.MaxValue - Seeds) throw new ArgumentException("Choose a nonnegative seed range within Int32.");
        if (!Enum.IsDefined(Candidate) || !Enum.IsDefined(Opponent) || !Enum.IsDefined(Cards) || !Enum.IsDefined(Setup))
            throw new ArgumentException("Choose a supported difficulty and ruleset.");
    }

    public static ArenaOptions Parse(string[] args)
    {
        if (args.Length % 2 != 0) throw new ArgumentException("Supply options as --name value pairs.");
        var options = new ArenaOptions();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!seen.Add(args[i])) throw new ArgumentException($"Duplicate option {args[i]}.");
            options = Apply(options, args[i], args[i + 1]);
        }
        options.Validate();
        return options;
    }

    private static ArenaOptions Apply(ArenaOptions options, string key, string value) => key switch
    {
        "--seeds" => options with { Seeds = int.Parse(value) },
        "--first-seed" => options with { FirstSeed = int.Parse(value) },
        "--players" => options with { Players = int.Parse(value) },
        "--candidate" => options with { Candidate = Choice<BotDifficulty>(value) },
        "--opponent" => options with { Opponent = Choice<BotDifficulty>(value) },
        "--cards" => options with { Cards = Choice<CardMode>(value) },
        "--setup" => options with { Setup = Choice<SetupMode>(value) },
        "--max-rounds" => options with { MaxRounds = int.Parse(value) },
        "--max-actions" => options with { MaxActions = int.Parse(value) },
        "--parallel" => options with { Parallelism = int.Parse(value) },
        _ => throw new ArgumentException($"Unknown option {key}."),
    };

    private static T Choice<T>(string value) where T : struct, Enum => Enum.GetNames<T>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
        ? Enum.Parse<T>(value, true) : throw new ArgumentException($"Unknown {typeof(T).Name}: {value}.");
}
