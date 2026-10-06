using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Server.Matches;

public sealed class RoomCommands(RoomRegistry registry, WorldMap map)
{
    public Room AddBot(string connection, BotDifficulty difficulty)
    {
        var room = registry.For(connection);
        lock (room.Sync)
        {
            RequireHost(room, connection);
            RequireLobby(room);
            if (!Enum.IsDefined(difficulty)) throw new HubException("Choose Easy, Normal, Hard, or Expert.");
            var id = Array.FindIndex(room.Seats, s => s == null);
            if (id < 0) throw new HubException("The table is full.");
            room.Seats[id] = new(id, $"{difficulty} AI {id + 1}") { IsBot = true, Difficulty = difficulty,
                Strategy = StrategyCatalog.Create(difficulty, RandomNumberGenerator.GetInt32(int.MaxValue)) };
            room.Changed();
            return room;
        }
    }

    public Room RemoveBot(string connection, int id)
    {
        var room = registry.For(connection);
        lock (room.Sync)
        {
            RequireHost(room, connection);
            RequireLobby(room);
            if (id is < 0 or > 5 || room.Seats[id]?.IsBot != true) throw new HubException("Choose an AI seat.");
            room.Seats[id] = null;
            room.Changed();
            return room;
        }
    }

    public Room Start(string connection)
    {
        var room = registry.For(connection);
        lock (room.Sync)
        {
            RequireHost(room, connection);
            RequireLobby(room);
            var seats = room.Occupied.ToArray();
            if (seats.Length < 2) throw new HubException(room.AiOnly ? "Add at least two AI players." : "Invite a friend or add an AI opponent.");
            if (seats.Any(s => !s.IsBot && s.Connection == null)) throw new HubException("Wait for every human player to reconnect.");
            for (var i = 0; i < seats.Length; i++) seats[i].GamePlayer = i;
            room.Game = new(map, seats.Select(s => s.Name).ToArray(), room.Options, new SeededRandom(RandomNumberGenerator.GetInt32(int.MaxValue)));
            room.Changed();
            return room;
        }
    }

    public Room Act(string connection, ActionRequest request)
    {
        var room = registry.For(connection);
        lock (room.Sync)
        {
            var seat = room.Members.First(s => s.Connection == connection);
            if (seat.IsSpectator) throw new HubException("Spectators cannot submit game moves.");
            if (room.Game == null) throw new HubException("Start the game first.");
            if (request == null || !Guid.TryParseExact(request.Id, "D", out var action)) throw new HubException("Invalid action identifier.");
            if (seat.AppliedActions.Contains(action)) return room;
            if (request.Revision != room.Revision) throw new HubException("The board changed. Try your action again.");
            try { room.Game.Apply(seat.GamePlayer, request.Command); }
            catch (RuleException error) { throw new HubException(error.Message); }
            seat.Remember(action);
            room.Changed();
            return room;
        }
    }

    private static void RequireLobby(Room room)
    {
        if (room.Game != null) throw new HubException("This game has already started.");
    }

    private static void RequireHost(Room room, string connection)
    {
        if (room.HostMember?.Connection != connection) throw new HubException("Only the host can change this table.");
    }
}
