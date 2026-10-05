using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

public enum CardMode
{
    [Display(Name = "Fixed")] Fixed,
    [Display(Name = "Progressive")] Progressive,
}
