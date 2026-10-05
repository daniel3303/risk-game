using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.AI;

public enum BotDifficulty
{
    [Display(Name = "Easy")] Easy,
    [Display(Name = "Normal")] Normal,
    [Display(Name = "Hard")] Hard,
}
