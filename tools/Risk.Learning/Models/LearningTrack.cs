using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning.Models;

/// <summary>
/// A rule set the learning tools record, fit and evaluate under, with the multiplayer model it ships. Features read the battle
/// odds of the rules, so a model is fitted and played under the rules of the games it was recorded in.
/// </summary>
/// <param name="Shipped">The bundled multiplayer model, or null for the hand-written terms when none is bundled yet.</param>
public sealed record LearningTrack(string Name, GameOptions Rules, Func<ValueModel> Shipped)
{
    /// <summary>Our lobby's rules: Fixed cards and true random dice; Ultimate's model.</summary>
    public static LearningTrack Ultimate { get; } = new("ultimate", new(CardMode.Fixed, SetupMode.Automatic), () => ValueModel.UltimateMultiplayer);

    /// <summary>The mobile game's regular public rooms: Fixed cards and balanced blitz; <see cref="Risk.Sim.AI.SmgStrategy"/>'s model.</summary>
    public static LearningTrack Smg { get; } = new("smg", new(CardMode.Fixed, SetupMode.Automatic, DiceMode.BalancedBlitz), () => ValueModel.SmgMultiplayer);

    public static IReadOnlyList<LearningTrack> All { get; } = [Ultimate, Smg];

    public static LearningTrack For(string name) =>
        All.FirstOrDefault(track => string.Equals(track.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown --track {name}; choose one of {string.Join(", ", All.Select(t => t.Name))}.");
}
