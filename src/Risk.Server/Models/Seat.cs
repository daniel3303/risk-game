using System.Security.Cryptography;
using Risk.Sim.AI;
namespace Risk.Server.Models;

public sealed class Seat(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; set; } = name;
    public string Token { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Connection { get; set; }
    public int GamePlayer { get; set; } = -1;
    public bool IsBot { get; set; }
    public bool IsSpectator { get; init; }
    public BotDifficulty Difficulty { get; set; }
    public IPlayerStrategy Strategy { get; set; }
    public HashSet<Guid> AppliedActions { get; } = [];
    public Queue<Guid> ActionHistory { get; } = new();

    public void Remember(Guid action)
    {
        AppliedActions.Add(action);
        ActionHistory.Enqueue(action);
        if (ActionHistory.Count > 100) AppliedActions.Remove(ActionHistory.Dequeue());
    }
}
