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

public sealed class GameHubTests : IClassFixture<GameFactory>
{
    private readonly GameFactory factory;
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;
    public GameHubTests(GameFactory factory) => this.factory = factory;

    [Fact]
    public async Task Health_RealPipeline_ReturnsHealthyWithSecurityHeaders()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/healthz", cancellation);
        response.IsSuccessStatusCode.Should().BeTrue();
        (await response.Content.ReadAsStringAsync(cancellation)).Should().Be("healthy");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'self'").And.NotContain("unsafe-eval");
    }

    [Fact]
    public async Task Start_NonHost_RejectsHostActionsAndOutOfTurnCommands()
    {
        await using var host = factory.Connection();
        await using var friend = factory.Connection();
        await host.StartAsync(cancellation);
        await friend.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("Create", "Host", new GameOptions(), cancellation);
        await friend.InvokeAsync<Welcome>("Join", welcome.Code, "Friend", null, cancellation);
        Func<Task> start = () => friend.InvokeAsync("Start", cancellation);
        await start.Should().ThrowAsync<HubException>().WithMessage("*Only the host*");
        Func<Task> add = () => friend.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await add.Should().ThrowAsync<HubException>().WithMessage("*Only the host*");
        await host.InvokeAsync("Start", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        var revision = room.Revision;
        Func<Task> wrongTurn = () => friend.InvokeAsync("Act", new ActionRequest(Guid.NewGuid().ToString(), revision,
            new() { Kind = CommandKind.EndAttack }), cancellation);
        await wrongTurn.Should().ThrowAsync<HubException>().WithMessage("*Wait for your turn*");
        room.Revision.Should().Be(revision);
    }

    [Fact]
    public async Task Act_DuplicateAndStaleRequests_ApplyExactlyOnce()
    {
        await using var host = factory.Connection();
        await using var friend = factory.Connection();
        await host.StartAsync(cancellation);
        await friend.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("Create", "Host", new GameOptions(), cancellation);
        await friend.InvokeAsync<Welcome>("Join", welcome.Code, "Friend", null, cancellation);
        await host.InvokeAsync("Start", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        var territory = room.Game.State.Territories.First(t => t.Owner == 0);
        var troops = territory.Troops;
        var request = new ActionRequest(Guid.NewGuid().ToString(), room.Revision, new() { Kind = CommandKind.Place, To = territory.Id, Count = 1 });
        await host.InvokeAsync("Act", request, cancellation);
        await host.InvokeAsync("Act", request, cancellation);
        territory.Troops.Should().Be(troops + 1);
        Func<Task> stale = () => host.InvokeAsync("Act", request with { Id = Guid.NewGuid().ToString() }, cancellation);
        await stale.Should().ThrowAsync<HubException>().WithMessage("*board changed*");
        territory.Troops.Should().Be(troops + 1);
    }

    [Fact]
    public async Task Snapshot_PerSeat_ExposesOnlyOwnCardsAndNoReconnectCredentials()
    {
        var hostMessages = Channel.CreateUnbounded<RoomSnapshot>();
        var friendMessages = Channel.CreateUnbounded<RoomSnapshot>();
        await using var host = factory.Connection(s => hostMessages.Writer.TryWrite(s));
        await using var friend = factory.Connection(s => friendMessages.Writer.TryWrite(s));
        await host.StartAsync(cancellation);
        await friend.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("Create", "Host", new GameOptions(), cancellation);
        await friend.InvokeAsync<Welcome>("Join", welcome.Code, "Friend", null, cancellation);
        await host.InvokeAsync("Start", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        lock (room.Sync) { room.Game.State.Players[1].Cards.Add(2); room.Changed(); }
        await factory.Services.GetRequiredService<RoomPublisher>().Publish(room, cancellation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var hostSnapshot = await Revision(hostMessages.Reader, room.Revision, timeout.Token);
        var friendSnapshot = await Revision(friendMessages.Reader, room.Revision, timeout.Token);
        hostSnapshot.Game.Hand.Should().BeEmpty();
        friendSnapshot.Game.Hand.Should().ContainSingle().Which.Id.Should().Be(2);
        hostSnapshot.Players.Single(p => p.Id == 1).Cards.Should().Be(1);
        Newtonsoft.Json.JsonConvert.SerializeObject(hostSnapshot).Should().NotContain(welcome.Token).And.NotContain("Deck");
    }

    [Fact]
    public async Task Join_DisconnectedSeat_ResumesWithTokenAndRejectsInvalidToken()
    {
        await using var original = factory.Connection();
        await original.StartAsync(cancellation);
        var first = await original.InvokeAsync<Welcome>("Create", "Commander", new GameOptions(), cancellation);
        await original.StopAsync(cancellation);
        await using var replacement = factory.Connection();
        await replacement.StartAsync(cancellation);
        Func<Task> wrong = () => replacement.InvokeAsync<Welcome>("Join", first.Code, "Intruder", "wrong-token", cancellation);
        await wrong.Should().ThrowAsync<HubException>().WithMessage("*expired*");
        var resumed = await replacement.InvokeAsync<Welcome>("Join", first.Code, "Commander", first.Token, cancellation);
        resumed.Seat.Should().Be(first.Seat);
        resumed.Token.Should().Be(first.Token);
        resumed.Snapshot.Players.Should().ContainSingle();
    }

    [Fact]
    public async Task Join_FullOrStartedRoom_RejectsAdditionalPlayers()
    {
        await using var host = factory.Connection();
        await using var visitor = factory.Connection();
        await host.StartAsync(cancellation);
        await visitor.StartAsync(cancellation);
        var welcome = await host.InvokeAsync<Welcome>("Create", "Commander", new GameOptions(), cancellation);
        for (var i = 0; i < 5; i++) await host.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        Func<Task> full = () => visitor.InvokeAsync<Welcome>("Join", welcome.Code, "Visitor", null, cancellation);
        await full.Should().ThrowAsync<HubException>().WithMessage("*six players*");
        await host.InvokeAsync("Start", cancellation);
        Func<Task> started = () => visitor.InvokeAsync<Welcome>("Join", welcome.Code, "Visitor", null, cancellation);
        await started.Should().ThrowAsync<HubException>().WithMessage("*already started*");
    }

    [Fact]
    public async Task Act_MalformedCommand_ReturnsHubErrorWithoutMutation()
    {
        await using var host = factory.Connection();
        await host.StartAsync(cancellation);
        await host.InvokeAsync<Welcome>("Create", "Commander", new GameOptions(), cancellation);
        await host.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await host.InvokeAsync("Start", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(host.ConnectionId);
        var revision = room.Revision;
        Func<Task> invalid = () => host.InvokeAsync("Act", new ActionRequest(Guid.NewGuid().ToString(), revision, null), cancellation);
        await invalid.Should().ThrowAsync<HubException>().WithMessage("*Unknown command*");
        room.Revision.Should().Be(revision);
    }

    private static async Task<RoomSnapshot> Revision(ChannelReader<RoomSnapshot> reader, long revision, CancellationToken cancellation)
    {
        while (true)
        {
            var snapshot = await reader.ReadAsync(cancellation);
            if (snapshot.Revision >= revision) return snapshot;
        }
    }

    [Fact]
    public async Task Join_HostLeftWhileFriendDisconnected_PromotesResumedFriend()
    {
        var messages = Channel.CreateUnbounded<RoomSnapshot>();
        await using var host = factory.Connection(s => messages.Writer.TryWrite(s));
        await using var friend = factory.Connection();
        await host.StartAsync(cancellation);
        await friend.StartAsync(cancellation);
        var first = await host.InvokeAsync<Welcome>("Create", "Host", new GameOptions(), cancellation);
        var invited = await friend.InvokeAsync<Welcome>("Join", first.Code, "Friend", null, cancellation);
        var nextRevision = invited.Snapshot.Revision + 1;
        await friend.StopAsync(cancellation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var disconnected = await Revision(messages.Reader, nextRevision, timeout.Token);
        disconnected.Players.Single(p => p.Id == invited.Seat).Connected.Should().BeFalse();
        await host.InvokeAsync("Leave", cancellation);
        await friend.StartAsync(cancellation);
        var resumed = await friend.InvokeAsync<Welcome>("Join", first.Code, "Friend", invited.Token, cancellation);
        resumed.Snapshot.Host.Should().Be(invited.Seat);
        await friend.InvokeAsync("AddBot", BotDifficulty.Easy, cancellation);
        await friend.InvokeAsync("Start", cancellation);
        var room = factory.Services.GetRequiredService<RoomRegistry>().For(friend.ConnectionId);
        room.Game.Should().NotBeNull();
    }
}
