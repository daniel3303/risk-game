using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using Risk.Sim;
using Risk.Sim.Models;
namespace Risk.Server.Matches;

public sealed class RoomRegistry
{
    private readonly object sync = new();
    private readonly Dictionary<string, Room> rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Room> memberships = new(StringComparer.Ordinal);
    private readonly HashSet<string> connections = [];

    public bool Connect(string connection)
    {
        lock (sync) return connections.Count < 512 && connections.Add(connection);
    }

    public Welcome Create(string connection, string name, GameOptions options)
    {
        lock (sync)
        {
            RequireFree(connection);
            if (options == null || !Enum.IsDefined(options.Cards) || !Enum.IsDefined(options.Setup)) throw new HubException("Choose valid rules.");
            name = CleanName(name);
            if (rooms.Count >= 64) throw new HubException("All tables are occupied. Try again shortly.");
            var code = NewCode();
            var room = new Room(code, options);
            var seat = new Seat(0, name);
            room.Seats[0] = seat;
            rooms.Add(code, room);
            return Attach(room, seat, connection);
        }
    }

    public Welcome Join(string connection, string code, string name, string token)
    {
        lock (sync)
        {
            RequireFree(connection);
            if (code == null || code.Length != 6 || !rooms.TryGetValue(code.ToUpperInvariant(), out var room)) throw new HubException("That six-letter room code was not found.");
            lock (room.Sync)
            {
                Seat seat;
                if (!string.IsNullOrEmpty(token))
                {
                    seat = room.Occupied.FirstOrDefault(s => !s.IsBot && s.Token == token);
                    if (seat == null) throw new HubException("Your saved seat has expired.");
                    if (seat.Connection != null) throw new HubException("This seat is already connected in another tab.");
                }
                else
                {
                    if (room.Game != null) throw new HubException("This game has already started.");
                    var slot = Array.FindIndex(room.Seats, s => s == null);
                    if (slot < 0) throw new HubException("This table has six players.");
                    seat = new Seat(slot, CleanName(name));
                    room.Seats[slot] = seat;
                }
                return Attach(room, seat, connection);
            }
        }
    }

    private Welcome Attach(Room room, Seat seat, string connection)
    {
        seat.Connection = connection;
        memberships.Add(connection, room);
        room.Changed();
        return new(room.Code, seat.Token, seat.Id, SnapshotBuilder.Build(room, seat));
    }

    public Room For(string connection)
    {
        lock (sync) return memberships.TryGetValue(connection, out var room) ? room : throw new HubException("Join a table first.");
    }

    public Room Leave(string connection, bool explicitLeave)
    {
        lock (sync)
        {
            if (!memberships.Remove(connection, out var room)) return null;
            lock (room.Sync)
            {
                var seat = room.Occupied.First(s => s.Connection == connection);
                seat.Connection = null;
                if (explicitLeave)
                {
                    if (room.Game == null) room.Seats[seat.Id] = null;
                    else
                    {
                        seat.IsBot = true;
                        seat.Difficulty = Sim.AI.BotDifficulty.Easy;
                        seat.Strategy = Sim.AI.StrategyCatalog.Create(seat.Difficulty, RandomNumberGenerator.GetInt32(int.MaxValue));
                        seat.Token = null;
                        seat.Name += " (AI)";
                    }
                }
                if (room.Host == seat.Id)
                    room.Host = room.Occupied.FirstOrDefault(s => !s.IsBot && s.Connection != null)?.Id ?? room.Host;
                room.Changed();
                if (room.Game == null && !room.Occupied.Any(s => !s.IsBot)) rooms.Remove(room.Code);
            }
            return room;
        }
    }

    public Room Disconnect(string connection)
    {
        var room = Leave(connection, false);
        lock (sync) connections.Remove(connection);
        return room;
    }

    public void Cleanup(DateTimeOffset now)
    {
        lock (sync)
        {
            foreach (var room in rooms.Values.ToArray())
                lock (room.Sync)
                    if (!room.Occupied.Any(s => s.Connection != null) && now - room.LastActive > TimeSpan.FromMinutes(30)) rooms.Remove(room.Code);
        }
    }

    private void RequireFree(string connection)
    {
        if (!connections.Contains(connection)) throw new HubException("Connection unavailable.");
        if (memberships.ContainsKey(connection)) throw new HubException("Leave your current table first.");
    }

    public static string CleanName(string name)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 24 || name.Any(char.IsControl)) throw new HubException("Use a name of 1–24 characters.");
        return name;
    }

    private string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        string code;
        do { code = new string(Enumerable.Range(0, 6).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray()); }
        while (rooms.ContainsKey(code));
        return code;
    }
}
