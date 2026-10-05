using System.ComponentModel.DataAnnotations;
namespace Risk.Sim.Models;

public enum CardSymbol
{
    [Display(Name = "Infantry")] Infantry,
    [Display(Name = "Cavalry")] Cavalry,
    [Display(Name = "Artillery")] Artillery,
    [Display(Name = "Wild")] Wild,
}
