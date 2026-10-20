using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

/// <summary>How attacks are settled: real dice, or the mobile game's balanced blitz (see <see cref="Rules.BalancedBlitz"/>).</summary>
public enum DiceMode
{
    [Display(Name = "True random")] TrueRandom,
    [Display(Name = "Balanced blitz")] BalancedBlitz,
}
