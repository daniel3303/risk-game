using Risk.Sim.AI.Planning;
using Risk.Sim.Models;
namespace Risk.Sim.AI;

/// <summary>Expert's turn planner with a wider, deeper capture search and weights tuned in two-player matches against Expert.</summary>
public sealed class MasterStrategy : IPlayerStrategy
{
    private readonly ExpertStrategy planner = new(ExpertTuning.Master);
    public string Id => "master-deep-planner";

    public GameCommand Choose(GameObservation observation) => planner.Choose(observation);
}
