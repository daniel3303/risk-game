using Risk.Sim.AI.Planning;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Sim.AI;

/// <summary>
/// Ultimate's planner and duel model for the mobile game's regular public rooms (Fixed cards, balanced blitz), with its own
/// multiplayer model trained in those games instead of Ultimate's; without a bundled one it plays the hand-written terms alone.
/// The lobby never offers it; the Arena, the learning tools and the SMG bridge seat it as <c>smg</c>.
/// </summary>
public sealed class SmgStrategy : IPlayerStrategy
{
    private readonly ExpertStrategy planner;

    public SmgStrategy(ValueModel multiplayer = null) =>
        planner = new(ExpertTuning.Ultimate with { Valuation = new UltimateValuation(ValueModel.Ultimate, multiplayer ?? ValueModel.SmgMultiplayer) });

    public string Id => "smg-learned-planner";

    public GameCommand Choose(GameObservation observation) => planner.Choose(observation);
}
