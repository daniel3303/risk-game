using Risk.Sim.Models;
namespace Risk.Server.Models;

public sealed record RoomSnapshot(string Code, long Revision, int Host, GameOptions Options, PlayerView[] Players, GameView Game);
