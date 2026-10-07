using Risk.Learning.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>Replays each starting deal of a Master mirror with different dice to split outcome variance between the deal and the dice.</summary>
public static class LuckAnalysis
{
    public static LuckSummary Run(WorldMap map, int firstSeed, int deals, int replays, int threads)
    {
        var rates = new double[deals];
        Parallel.For(0, deals, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var wins = 0;
            for (var r = 0; r < replays; r++)
            {
                var random = new SwitchingRandom(firstSeed + i);
                var game = new Game(map, ["A", "B"], new(CardMode.Fixed, SetupMode.Automatic), random);
                // The deal (territories, armies and card order) is fixed by now; every later draw, such as dice, differs per replay.
                random.Switch(HashCode.Combine(firstSeed + i, r));
                var master = new MasterStrategy();
                var actions = 0;
                while (game.State.Phase != Phase.Finished && actions++ < 20000)
                    game.Apply(game.State.CurrentPlayer, master.Choose(GameObservation.From(game)));
                if (game.State.Winner == 0) wins++;
            }
            rates[i] = wins / (double)replays;
        });
        var mean = rates.Average();
        var total = mean * (1 - mean);
        // Between-deal variance, corrected for the sampling noise of a finite number of replays.
        var deal = Math.Max(0, rates.Average(r => (r - mean) * (r - mean)) - rates.Average(r => r * (1 - r)) / (replays - 1));
        return new(deals, replays, mean, rates.Count(r => r >= .9) / (double)deals, rates.Count(r => r <= .1) / (double)deals,
            rates.Count(r => r is >= .3 and <= .7) / (double)deals, total, deal, total - deal);
    }
}
