using Risk.Server.Models;
using Risk.Sim;
using Risk.Sim.Models;
namespace Risk.Server.Matches;

public sealed class Room(string code, GameOptions options, bool aiOnly = false)
{
    public object Sync { get; } = new();
    public string Code { get; } = code;
    public GameOptions Options { get; } = options;
    public bool AiOnly { get; } = aiOnly;
    public Seat[] Seats { get; } = new Seat[6];
    public List<Seat> Spectators { get; } = [];
    public Game Game { get; set; }
    public int Host { get; set; }
    public long Revision { get; set; }
    public bool BotQueued { get; set; }
    public DateTimeOffset LastActive { get; set; } = DateTimeOffset.UtcNow;
    public IEnumerable<Seat> Occupied => Seats.Where(s => s != null);
    public IEnumerable<Seat> Members => Occupied.Concat(Spectators);
    public Seat HostMember => Members.FirstOrDefault(s => s.Id == Host);
    public bool HasViewers => Members.Any(s => s.Connection != null);
    public Seat Current => Game == null ? null : Occupied.First(s => s.GamePlayer == Game.State.CurrentPlayer);
    public int PublicPlayer(int player) => player < 0 ? -1 : Occupied.First(s => s.GamePlayer == player).Id;

    public void Changed() { Revision++; LastActive = DateTimeOffset.UtcNow; }
}
