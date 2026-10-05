using Risk.Sim.Models;
namespace Risk.Sim.Rules;

public static class GameSetup
{
    private static readonly int[] InitialArmies = [0, 0, 40, 35, 30, 25, 20];

    public static GameState Create(WorldMap map, string[] names, GameOptions options, IRandomSource random)
    {
        if (names.Length is < 2 or > 6) throw new RuleException("Choose two to six players.");
        if (!Enum.IsDefined(options.Cards) || !Enum.IsDefined(options.Setup)) throw new RuleException("Invalid rules.");
        var state = new GameState
        {
            Players = names.Select((name, id) => new PlayerState(id, name) { SetupTroops = InitialArmies[names.Length] }).ToArray(),
            Territories = map.Territories.Select(t => new TerritoryState(t.Id)).ToArray(),
            Phase = options.Setup == SetupMode.Manual ? Phase.Claim : Phase.Draft,
        };
        foreach (var id in Shuffle(Enumerable.Range(0, 44), random)) state.Deck.Enqueue(id);
        if (options.Setup == SetupMode.Automatic) Distribute(state, random);
        return state;
    }

    private static void Distribute(GameState state, IRandomSource random)
    {
        var shuffled = Shuffle(Enumerable.Range(0, 42), random);
        for (var i = 0; i < shuffled.Length; i++)
        {
            var player = i % state.Players.Length;
            state.Territories[shuffled[i]].Owner = player;
            state.Territories[shuffled[i]].Troops = 1;
            state.Players[player].SetupTroops--;
        }
        foreach (var player in state.Players)
        {
            var owned = state.Territories.Where(t => t.Owner == player.Id).ToArray();
            while (player.SetupTroops > 0) { owned[random.Next(owned.Length)].Troops++; player.SetupTroops--; }
        }
    }

    public static int[] Shuffle(IEnumerable<int> ids, IRandomSource random)
    {
        var shuffled = ids.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }

    public static void Claim(GameState state, TerritoryState territory)
    {
        if (state.Phase != Phase.Claim || territory.Owner != -1) throw new RuleException("Choose an unclaimed territory.");
        territory.Owner = state.CurrentPlayer;
        territory.Troops = 1;
        state.Players[state.CurrentPlayer].SetupTroops--;
        state.CurrentPlayer = (state.CurrentPlayer + 1) % state.Players.Length;
        if (state.Territories.All(t => t.Owner >= 0)) { state.Phase = Phase.Setup; state.CurrentPlayer = 0; }
    }

    public static void Place(GameState state, TerritoryState territory, int count)
    {
        if (count != 1) throw new RuleException("Place one starting troop at a time.");
        var player = state.Players[state.CurrentPlayer];
        if (player.SetupTroops <= 0) throw new RuleException("No starting troops remain.");
        territory.Troops++;
        player.SetupTroops--;
        if (state.Players.All(p => p.SetupTroops == 0)) { state.CurrentPlayer = 0; state.Phase = Phase.Draft; return; }
        do { state.CurrentPlayer = (state.CurrentPlayer + 1) % state.Players.Length; }
        while (state.Players[state.CurrentPlayer].SetupTroops == 0);
    }
}
