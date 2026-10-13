using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Sim.AI;

/// <summary>
/// Master's planner with value models learned from self-play correcting its evaluation, and with frontier defence, threat
/// weighting and siding with bots against people when several rivals remain; the reserved home of the strongest trained model.
/// </summary>
public sealed class UltimateStrategy : IPlayerStrategy
{
    private readonly ExpertStrategy planner = new(ExpertTuning.Ultimate with { Valuation = UltimateValuation.Bundled });
    public string Id => "ultimate-learned-planner";

    public GameCommand Choose(GameObservation observation) => planner.Choose(observation);
}
