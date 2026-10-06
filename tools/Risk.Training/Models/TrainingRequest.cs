namespace Risk.Training.Models;

public sealed record TrainingRequest(string Operation, int Seed = 0, int Seat = 0, string Opponent = "expert", int Action = -1, int MaxRounds = 100, int MaxActions = 5000)
{
    // Newtonsoft uses the parameterless constructor to preserve omitted option defaults.
    public TrainingRequest() : this(Operation: null) { }
}
