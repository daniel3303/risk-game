using AwesomeAssertions;
using Risk.Server.Matches;
using Risk.Sim.Models;
using Xunit;
namespace Risk.IntegrationTests;

public sealed class RoomTests
{
    [Fact]
    public void HumanPlayers_MixedSeats_ListsOnlyPeopleHoldingAGamePlayer()
    {
        var room = new Room("ROOM", new GameOptions());
        room.Seats[0] = new(0, "Bot") { IsBot = true, GamePlayer = 0 };
        room.Seats[1] = new(1, "Ana") { GamePlayer = 2 };
        room.Seats[2] = new(2, "Left (AI)") { IsBot = true, GamePlayer = 1 };
        room.Seats[3] = new(3, "Lobby");
        room.HumanPlayers.Should().Equal(2);
    }
}
