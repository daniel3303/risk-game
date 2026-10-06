using Microsoft.AspNetCore.SignalR;
using Risk.Server.Matches;
using Risk.Server.Models;
using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Server.Hubs;

public sealed class GameHub(RoomRegistry registry, RoomCommands commands, RoomPublisher publisher, BotRunner bots) : Hub
{
    public override Task OnConnectedAsync()
    {
        if (!registry.Connect(Context.ConnectionId)) Context.Abort();
        return base.OnConnectedAsync();
    }

    public async Task<Welcome> Create(string name, GameOptions options)
    {
        var welcome = registry.Create(Context.ConnectionId, name, options);
        await Changed(registry.For(Context.ConnectionId));
        return welcome;
    }

    public async Task<Welcome> CreateSpectatorRoom(string name, GameOptions options)
    {
        var welcome = registry.Create(Context.ConnectionId, name, options, aiOnly: true);
        await Changed(registry.For(Context.ConnectionId));
        return welcome;
    }

    public async Task<Welcome> Join(string code, string name, string token = null)
    {
        var welcome = registry.Join(Context.ConnectionId, code, name, token);
        await Changed(registry.For(Context.ConnectionId));
        return welcome;
    }

    public Task AddBot(BotDifficulty difficulty) => Changed(commands.AddBot(Context.ConnectionId, difficulty));
    public Task RemoveBot(int seat) => Changed(commands.RemoveBot(Context.ConnectionId, seat));
    public Task Start() => Changed(commands.Start(Context.ConnectionId));
    public Task Act(ActionRequest request) => Changed(commands.Act(Context.ConnectionId, request));
    public Task Leave() => Changed(registry.Leave(Context.ConnectionId, true));
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        await Changed(registry.Disconnect(Context.ConnectionId));
        await base.OnDisconnectedAsync(exception);
    }

    private async Task Changed(Room room)
    {
        await publisher.Publish(room);
        bots.Schedule(room);
    }
}
