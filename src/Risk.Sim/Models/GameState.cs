namespace Risk.Sim.Models;

public sealed class GameState
{
    public PlayerState[] Players { get; init; } = [];
    public TerritoryState[] Territories { get; init; } = [];
    public Phase Phase { get; set; }
    public int CurrentPlayer { get; set; }
    public int Round { get; set; } = 1;
    public int Reinforcements { get; set; }
    public int Trades { get; set; }
    public bool ConqueredThisTurn { get; set; }
    public bool ResumeAttack { get; set; }
    public int Winner { get; set; } = -1;
    public Capture Capture { get; set; }
    public BattleResult Battle { get; set; }
    public Queue<int> Deck { get; } = new();
    public List<string> Log { get; } = [];

    public void Record(string message)
    {
        Log.Add(message);
        if (Log.Count > 40) Log.RemoveAt(0);
    }
}
