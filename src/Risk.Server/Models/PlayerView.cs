using Risk.Sim.AI;
namespace Risk.Server.Models;

public sealed record PlayerView(int Id, string Name, bool IsBot, BotDifficulty Difficulty, string Strategy,
    bool Connected, bool Eliminated, int Cards, int Territories, int Troops);
