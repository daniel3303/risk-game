using Risk.Sim.Models;
namespace Risk.Sim.AI;

public interface IPlayerStrategy
{
    string Id { get; }
    GameCommand Choose(GameObservation observation);
}
