namespace Risk.Sim.AI;

public static class StrategyCatalog
{
    public static IPlayerStrategy Create(BotDifficulty difficulty, int seed) => difficulty switch
    {
        BotDifficulty.Easy => new HeuristicStrategy(true),
        BotDifficulty.Normal => new HeuristicStrategy(false),
        BotDifficulty.Hard => new MonteCarloStrategy(new SeededRandom(seed)),
        _ => throw new RuleException("Choose a valid AI difficulty."),
    };
}
