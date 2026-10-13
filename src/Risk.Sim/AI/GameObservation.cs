using Risk.Sim.Models;
namespace Risk.Sim.AI;

public sealed record GameObservation(WorldMap Map, Phase Phase, int Player, int Reinforcements, int[] Cards,
    ObservedTerritory[] Territories, Capture Capture, bool ConqueredThisTurn,
    GameOptions Options = null, int Trades = 0, ObservedPlayer[] Players = null)
{
    /// <param name="humans">The seats a person plays, flagged on <see cref="Players"/>; null when there are none.</param>
    public static GameObservation From(Game game, IReadOnlyCollection<int> humans = null) => new(game.Map, game.State.Phase, game.State.CurrentPlayer,
        game.State.Reinforcements, game.State.Players[game.State.CurrentPlayer].Cards.ToArray(),
        game.State.Territories.Select(t => new ObservedTerritory(t.Id, t.Owner, t.Troops)).ToArray(),
        game.State.Capture, game.State.ConqueredThisTurn, game.Options, game.State.Trades,
        game.State.Players.Select(p => new ObservedPlayer(p.Id, p.Eliminated, p.Cards.Count, humans?.Contains(p.Id) == true)).ToArray());
}
