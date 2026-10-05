using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

public enum SetupMode
{
    [Display(Name = "Automatic")] Automatic,
    [Display(Name = "Manual")] Manual,
}
