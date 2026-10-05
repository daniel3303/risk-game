using Risk.Sim.Models;
namespace Risk.Sim.AI;

public sealed record GameObservation(WorldMap Map, Phase Phase, int Player, int Reinforcements, int[] Cards,
    ObservedTerritory[] Territories, Capture Capture, bool ConqueredThisTurn)
{
    public static GameObservation From(Game game) => new(game.Map, game.State.Phase, game.State.CurrentPlayer,
        game.State.Reinforcements, game.State.Players[game.State.CurrentPlayer].Cards.ToArray(),
        game.State.Territories.Select(t => new ObservedTerritory(t.Id, t.Owner, t.Troops)).ToArray(),
        game.State.Capture, game.State.ConqueredThisTurn);
}
