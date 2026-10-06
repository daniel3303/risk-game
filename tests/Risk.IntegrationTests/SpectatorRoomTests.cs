using System.Threading.Channels;
using AwesomeAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Risk.Server.Matches;
using Risk.Server.Models;
using Risk.Sim.AI;
using Risk.Sim.Models;
using Xunit;
namespace Risk.IntegrationTests;

public sealed class SpectatorRoomTests(GameFactory factory) : IClassFixture<GameFactory>
{
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateSpectatorRoom_SixBots_HostDoesNotOccupyACommanderSeat()
    {
        await using var host = factory.Connection();
        await host.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("CreateSpectatorRoom", "Observer", new GameOptions(), cancellation);
        welcome.Snapshot.AiOnly.Should().BeTrue();
        welcome.Snapshot.Players.Should().BeEmpty();
        welcome.Snapshot.Spectators.Should().ContainSingle().Which.Id.Should().Be(welcome.Seat);
        welcome.Snapshot.Host.Should().Be(welcome.Seat);
        Func<Task> empty = () => host.InvokeAsync("Start", cancellation);
        await empty.Should().ThrowAsync<HubException>().WithMessage("*at least two AI*");
        for (var i = 0; i < 6; i++) await host.InvokeAsync("AddBot", (BotDifficulty)(i % 4), cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        room.Occupied.Should().HaveCount(6).And.OnlyContain(s => s.IsBot);
        room.Occupied.Select(s => s.Id).Should().Equal(0, 1, 2, 3, 4, 5);
        Func<Task> full = () => host.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await full.Should().ThrowAsync<HubException>().WithMessage("*table is full*");
        await host.InvokeAsync("RemoveBot", 3, cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Hard, cancellation);
        await host.InvokeAsync("Start", cancellation);
        room.Game.State.Players.Should().HaveCount(6);
    }

    [Fact]
    public async Task Join_StartedAiRoom_SpectatorsHaveNoCardsOrGameplayPermission()
    {
        await using var host = factory.Connection();
        await using var guest = factory.Connection();
        await host.StartAsync(cancellation);
        await guest.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("CreateSpectatorRoom", "Host", new GameOptions(), cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Normal, cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        lock (room.Sync) room.BotQueued = true;
        await host.InvokeAsync("Start", cancellation);
        lock (room.Sync) { room.Game.State.Players[0].Cards.Add(2); room.Changed(); }
        var joined = await guest.InvokeAsync<Welcome>("Join", welcome.Code, "Guest", null, cancellation);
        joined.Snapshot.Players.Should().HaveCount(2).And.OnlyContain(p => p.IsBot);
        joined.Snapshot.Spectators.Should().HaveCount(2);
        joined.Snapshot.Game.Hand.Should().BeEmpty();
        joined.Snapshot.Players.Single(p => p.Id == 0).Cards.Should().Be(1);
        Newtonsoft.Json.JsonConvert.SerializeObject(joined.Snapshot).Should().NotContain(welcome.Token).And.NotContain(joined.Token).And.NotContain("Deck");
        var revision = room.Revision;
        foreach (var connection in new[] { host, guest })
        {
            Func<Task> act = () => connection.InvokeAsync("Act", new ActionRequest(Guid.NewGuid().ToString(), revision,
                new() { Kind = CommandKind.Surrender }), cancellation);
            await act.Should().ThrowAsync<HubException>().WithMessage("*Spectators cannot submit*");
        }
        room.Revision.Should().Be(revision);
        Func<Task> add = () => guest.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await add.Should().ThrowAsync<HubException>().WithMessage("*Only the host*");
        await guest.InvokeAsync("Leave", cancellation);
        room.Occupied.Should().HaveCount(2);
        room.Spectators.Should().ContainSingle();
    }

    [Fact]
    public async Task Leave_SpectatorHostInLobby_PromotesAnotherSpectator()
    {
        await using var host = factory.Connection();
        await using var guest = factory.Connection();
        await host.StartAsync(cancellation);
        await guest.StartAsync(cancellation);
        var first = await host.InvokeAsync<Welcome>("CreateSpectatorRoom", "Host", new GameOptions(), cancellation);
        var joined = await guest.InvokeAsync<Welcome>("Join", first.Code, "Guest", null, cancellation);
        Func<Task> unauthorized = () => guest.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await unauthorized.Should().ThrowAsync<HubException>().WithMessage("*Only the host*");
        await host.InvokeAsync("Leave", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(guest.ConnectionId);
        room.Host.Should().Be(joined.Seat);
        await guest.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await guest.InvokeAsync("AddBot", BotDifficulty.Hard, cancellation);
        await guest.InvokeAsync("Start", cancellation);
        room.Game.Should().NotBeNull();
    }

    [Fact]
    public async Task Bots_OnlySpectatorConnected_AdvanceAndResumeAfterReconnect()
    {
        var messages = Channel.CreateUnbounded<RoomSnapshot>();
        await using var host = factory.Connection(s => messages.Writer.TryWrite(s));
        await host.StartAsync(cancellation);
        var first = await host.InvokeAsync<Welcome>("CreateSpectatorRoom", "Observer", new GameOptions(), cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Normal, cancellation);
        var registry = factory.Services.GetRequiredService<RoomRegistry>();
        var room = registry.For(host.ConnectionId);
        var startedRevision = room.Revision + 1;
        await host.InvokeAsync("Start", cancellation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var advanced = await Revision(messages.Reader, startedRevision + 1, timeout.Token);
        advanced.Game.Should().NotBeNull();
        advanced.Game.Hand.Should().BeEmpty();
        await host.StopAsync(cancellation);
        while (true)
        {
            lock (room.Sync) if (!room.HasViewers && !room.BotQueued) break;
            await Task.Delay(20, timeout.Token);
        }
        var pausedRevision = room.Revision;
        await using var resumed = factory.Connection(s => messages.Writer.TryWrite(s));
        await resumed.StartAsync(cancellation);
        Func<Task> wrong = () => resumed.InvokeAsync<Welcome>("Join", first.Code, "Observer", "bad-token", cancellation);
        await wrong.Should().ThrowAsync<HubException>().WithMessage("*expired*");
        var welcome = await resumed.InvokeAsync<Welcome>("Join", first.Code, "Observer", first.Token, cancellation);
        welcome.Seat.Should().Be(first.Seat);
        welcome.Token.Should().Be(first.Token);
        welcome.Snapshot.Host.Should().Be(first.Seat);
        welcome.Snapshot.Players.Should().HaveCount(2).And.OnlyContain(p => p.IsBot);
        (await Revision(messages.Reader, pausedRevision + 2, timeout.Token)).Game.Hand.Should().BeEmpty();
    }

    private static async Task<RoomSnapshot> Revision(ChannelReader<RoomSnapshot> reader, long revision, CancellationToken cancellation)
    {
        while (true)
        {
            var snapshot = await reader.ReadAsync(cancellation);
            if (snapshot.Revision >= revision) return snapshot;
        }
    }
}
