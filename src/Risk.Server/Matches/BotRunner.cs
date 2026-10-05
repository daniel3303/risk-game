using System.Threading.Channels;
using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Server.Matches;

public sealed class BotRunner(RoomPublisher publisher, ILogger<BotRunner> logger) : BackgroundService
{
    private readonly Channel<Room> queue = Channel.CreateBounded<Room>(64);

    public void Schedule(Room room)
    {
        if (room == null) return;
        lock (room.Sync)
        {
            if (room.BotQueued || room.Game == null || room.Game.State.Phase == Phase.Finished || room.Current?.IsBot != true) return;
            if (!room.Occupied.Any(s => s.Connection != null)) return;
            room.BotQueued = queue.Writer.TryWrite(room);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Work(stoppingToken)));

    private async Task Work(CancellationToken cancellation)
    {
        await foreach (var room in queue.Reader.ReadAllAsync(cancellation))
        {
            await Task.Delay(400, cancellation);
            GameObservation observation;
            IPlayerStrategy strategy;
            long revision;
            lock (room.Sync)
            {
                if (room.Game.State.Phase == Phase.Finished || !room.Current.IsBot || !room.Occupied.Any(s => s.Connection != null))
                { room.BotQueued = false; continue; }
                strategy = room.Current.Strategy;
                observation = GameObservation.From(room.Game);
                revision = room.Revision;
            }
            try
            {
                var command = strategy.Choose(observation);
                lock (room.Sync)
                {
                    if (room.Revision == revision)
                    {
                        room.Game.Apply(observation.Player, command);
                        room.Changed();
                    }
                    room.BotQueued = false;
                }
            }
            catch (Sim.RuleException error)
            {
                lock (room.Sync) room.BotQueued = false;
                logger.LogError(error, "Strategy {Strategy} produced an invalid command in room {Room}", strategy.Id, room.Code);
                continue;
            }
            await publisher.Publish(room, cancellation);
            Schedule(room);
        }
    }
}
