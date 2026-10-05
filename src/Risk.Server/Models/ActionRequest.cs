using Risk.Sim.Models;
namespace Risk.Server.Models;

public sealed record ActionRequest(string Id, long Revision, GameCommand Command);
