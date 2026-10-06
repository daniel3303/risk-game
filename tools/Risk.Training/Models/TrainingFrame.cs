using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Training.Models;

public sealed record TrainingFrame(EncodedObservation Observation, int Player, bool Terminated, bool Truncated, double Reward,
    int Winner, int Round, int Actions, int Teacher = -1, GameCommand Decision = null);
