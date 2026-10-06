using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;
namespace Risk.Server.Matches;

public sealed class RoomPublisher(IHubContext<GameHub> hub)
{
    public Task Publish(Room room, CancellationToken cancellation = default)
    {
        if (room == null) return Task.CompletedTask;
        (string Connection, RoomSnapshot Snapshot)[] messages;
        lock (room.Sync)
            messages = room.Members.Where(s => s.Connection != null).Select(s => (s.Connection, SnapshotBuilder.Build(room, s))).ToArray();
        return Task.WhenAll(messages.Select(m => hub.Clients.Client(m.Connection).SendAsync("Snapshot", m.Snapshot, cancellation)));
    }
}
