using Risk.Sim.Models;
namespace Risk.Server.Models;

public sealed record GameView(Phase Phase, int CurrentPlayer, int Round, int Reinforcements, int Trades,
    int Winner, int SetupTroops, bool ForcedTrade, TerritoryView[] Territories, TerritoryCard[] Hand,
    Capture Capture, BattleResult Battle, string[] Log);
