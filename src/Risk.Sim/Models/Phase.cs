using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

public enum Phase
{
    [Display(Name = "Claim territories")] Claim,
    [Display(Name = "Place starting troops")] Setup,
    [Display(Name = "Draft")] Draft,
    [Display(Name = "Attack")] Attack,
    [Display(Name = "Move after capture")] Occupy,
    [Display(Name = "Fortify")] Fortify,
    [Display(Name = "Finished")] Finished,
}
