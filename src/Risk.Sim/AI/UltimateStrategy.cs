using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Sim.AI;

/// <summary>Master's planner with a value model learned from self-play correcting its two-player evaluation; the reserved home of the strongest trained model.</summary>
public sealed class UltimateStrategy : IPlayerStrategy
{
    private readonly ExpertStrategy planner = new(ExpertTuning.Master with { Valuation = ValueModel.Ultimate });
    public string Id => "ultimate-learned-planner";

    public GameCommand Choose(GameObservation observation) => planner.Choose(observation);
}
