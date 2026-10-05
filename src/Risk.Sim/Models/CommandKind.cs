using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

public enum CommandKind
{
    [Display(Name = "Claim")] Claim,
    [Display(Name = "Place troops")] Place,
    [Display(Name = "Trade cards")] Trade,
    [Display(Name = "Attack")] Attack,
    [Display(Name = "Occupy")] Occupy,
    [Display(Name = "End attack")] EndAttack,
    [Display(Name = "Fortify")] Fortify,
    [Display(Name = "End turn")] EndTurn,
    [Display(Name = "Surrender")] Surrender,
}
