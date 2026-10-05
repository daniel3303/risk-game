using Risk.Sim.Models;
namespace Risk.Sim.Rules;

public static class TurnRules
{
    public static int Reinforcements(WorldMap map, GameState state, int player)
    {
        var owned = state.Territories.Where(t => t.Owner == player).Select(t => t.Id).ToHashSet();
        var bonus = map.Continents.Where(c => map.Territories.Where(t => t.Continent == c.Id).All(t => owned.Contains(t.Id))).Sum(c => c.Bonus);
        return Math.Max(3, owned.Count / 3) + bonus;
    }

    public static void Begin(WorldMap map, GameState state)
    {
        state.Phase = Phase.Draft;
        state.Reinforcements = Reinforcements(map, state, state.CurrentPlayer);
        state.ConqueredThisTurn = false;
        state.ResumeAttack = false;
        state.Capture = null;
        state.Record($"{state.Players[state.CurrentPlayer].Name} drafts {state.Reinforcements} troops.");
    }

    public static void Place(GameState state, TerritoryState territory, int count)
    {
        if (count < 1 || count > state.Reinforcements) throw new RuleException("Choose an available number of troops.");
        if (state.Players[state.CurrentPlayer].Cards.Count >= 5) throw new RuleException("Trade a card set before placing troops.");
        territory.Troops += count;
        state.Reinforcements -= count;
        if (state.Reinforcements == 0) state.Phase = Phase.Attack;
    }

    public static void End(WorldMap map, GameState state)
    {
        if (state.ConqueredThisTurn && state.Deck.Count > 0) state.Players[state.CurrentPlayer].Cards.Add(state.Deck.Dequeue());
        Advance(state);
        Begin(map, state);
    }

    public static void Advance(GameState state)
    {
        do
        {
            state.CurrentPlayer = (state.CurrentPlayer + 1) % state.Players.Length;
            if (state.CurrentPlayer == 0) state.Round++;
        } while (state.Players[state.CurrentPlayer].Eliminated);
    }

    public static bool CheckWinner(GameState state)
    {
        var active = state.Players.Where(p => !p.Eliminated).ToArray();
        if (active.Length != 1) return false;
        state.Winner = active[0].Id;
        state.Phase = Phase.Finished;
        state.Record($"{active[0].Name} wins World Domination.");
        return true;
    }
}
