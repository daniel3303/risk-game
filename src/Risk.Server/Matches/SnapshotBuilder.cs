using Risk.Server.Models;
using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Server.Matches;

public static class SnapshotBuilder
{
    public static RoomSnapshot Build(Room room, Seat viewer)
    {
        var players = room.Occupied.Select(s => Player(room, s)).ToArray();
        var game = room.Game == null ? null : Game(room, viewer);
        var spectators = room.Spectators.Select(s => new SpectatorView(s.Id, s.Name, s.Connection != null)).ToArray();
        return new(room.Code, room.Revision, room.Host, room.Options, players, game, room.AiOnly, spectators);
    }

    private static PlayerView Player(Room room, Seat seat)
    {
        var state = room.Game?.State;
        var owned = state?.Territories.Where(t => t.Owner == seat.GamePlayer).ToArray() ?? [];
        var player = state?.Players[seat.GamePlayer];
        return new(seat.Id, seat.Name, seat.IsBot, seat.Difficulty, seat.Strategy?.Id,
            seat.Connection != null, player?.Eliminated ?? false, player?.Cards.Count ?? 0, owned.Length, owned.Sum(t => t.Troops));
    }

    private static GameView Game(Room room, Seat viewer)
    {
        var state = room.Game.State;
        var current = state.Players[state.CurrentPlayer];
        return new(state.Phase, room.PublicPlayer(state.CurrentPlayer), state.Round, state.Reinforcements, state.Trades,
            room.PublicPlayer(state.Winner), current.SetupTroops, state.Phase == Phase.Draft && current.Cards.Count >= 5,
            state.Territories.Select(t => new TerritoryView(t.Id, room.PublicPlayer(t.Owner), t.Troops)).ToArray(),
            viewer.IsSpectator ? [] : state.Players[viewer.GamePlayer].Cards.Select(CardRules.Card).ToArray(), state.Capture, state.Battle, state.Log.ToArray());
    }
}
