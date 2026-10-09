using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Risk.Learning.Models;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// One self-improvement generation for games with more than two players: record league self-play with the current model, fit the
/// next value network bootstrapped from it, then test the candidate against the current Ultimate and against the turtle probe.
/// The candidate is promoted only when it beats the current Ultimate with its conservative lower bound above the fair share and
/// the probe is not demonstrably stronger against it than against the current preset on the same seeds.
/// </summary>
public static class Improvement
{
    public sealed record Options(int Players, int FirstSeed, int Games, string Output, ValueModel Current, int Hidden, int Epochs, double Scale, double Limit,
        int EvaluationSeeds, int ProbeSeeds, string PromoteTo, int Threads, int FitSeed = 3);

    public sealed record Report(int Players, int FirstSeed, int Games, int Afterstates, string Model, EvaluationSummary AgainstUltimate,
        EvaluationSummary ProbeAgainstCandidate, EvaluationSummary ProbeAgainstCurrent, bool Promoted, string PromotedTo);

    public static Report Run(WorldMap map, Options options, TextWriter log)
    {
        // Strength is measured against the bundled Ultimate, so the model it carries must be the one the probe baseline uses.
        if ((options.Current?.Serialize() ?? "") != (ValueModel.UltimateMultiplayer?.Serialize() ?? ""))
            throw new InvalidOperationException("--model must be the multiplayer model bundled in this build; promote it, rebuild, then run the next generation.");
        if (File.Exists(Path.Combine(options.Output, "improve.json")))
            throw new InvalidOperationException($"{options.Output} already holds a finished generation; give the next one a new --output and unused seeds.");
        Directory.CreateDirectory(options.Output);
        var format = TrajectoryFormat.For(options.Players);
        // A resumed run must never read rows of another width, games played by another learner, or seeds the gate will use.
        var data = Path.Combine(options.Output, $"data-{format.Encoding.Version}-{Learner(options.Current)}-{options.FirstSeed}-{options.Games}.bin");
        log.WriteLine($"Generation at {options.Output}: {options.Games} {options.Players}-player games from seed {options.FirstSeed}, learner = {(options.Current == null ? "current preset" : "current model")}.");
        TrajectoryRecorder.Record(map, options.Players, options.FirstSeed, options.Games, data, options.Current, options.Threads, log);
        var rows = format.Read([data]);
        var network = new ValueTrainer(options.Hidden, options.Epochs, .001, 1e-5, .7, options.FitSeed, options.Threads, log).Fit(map, rows, options.Current, format);
        var candidate = new ValueModel { Features = format.Encoding.Version, Scale = options.Scale, Limit = options.Limit, Members = [network] };
        var modelPath = Path.Combine(options.Output, "model.json");
        File.WriteAllText(modelPath, candidate.Serialize());
        // Evaluation seeds follow the recorded ones so they were never played before this generation.
        var evaluationSeed = options.FirstSeed + options.Games;
        var probeSeed = evaluationSeed + options.EvaluationSeeds;
        log.WriteLine($"Evaluating the candidate against Ultimate on seeds {evaluationSeed}+{options.EvaluationSeeds} and against the turtle probe on {probeSeed}+{options.ProbeSeeds}.");
        var against = ModelEvaluator.Run(map, candidate, "ultimate", evaluationSeed, options.EvaluationSeeds, options.Players, false, options.Threads);
        var probeCandidate = ModelEvaluator.Run(map, candidate, "turtle", probeSeed, options.ProbeSeeds, options.Players, true, options.Threads);
        var probeCurrent = ModelEvaluator.Run(map, options.Current ?? Empty(format), "turtle", probeSeed, options.ProbeSeeds, options.Players, true, options.Threads);
        // The probe check rejects only a demonstrable regression: with 900 games its rate moves several points on noise alone.
        var promoted = against.ConfidenceLower > against.FairShare && probeCandidate.ConfidenceLower <= probeCurrent.WinRate;
        string promotedTo = null;
        if (promoted && options.PromoteTo != null)
        {
            File.Copy(modelPath, options.PromoteTo, true);
            promotedTo = options.PromoteTo;
            log.WriteLine($"Promoted: copied the candidate to {options.PromoteTo}; rebuild to play it.");
        }
        var report = new Report(options.Players, options.FirstSeed, options.Games, rows.Length, modelPath, against, probeCandidate, probeCurrent, promoted, promotedTo);
        File.WriteAllText(Path.Combine(options.Output, "improve.json"), JsonConvert.SerializeObject(report, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
        return report;
    }

    private static string Learner(ValueModel model) =>
        model == null ? "hand" : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(model.Serialize())))[..12];

    /// <summary>A model whose correction is always zero, so the current preset can be evaluated through the same path as a candidate.</summary>
    private static ValueModel Empty(TrajectoryFormat format) => new()
    {
        Features = format.Encoding.Version, Scale = 0,
        Members = [new ValueNetwork { Inputs = format.Encoding.Count, Hidden = 1, A = 1, W = new float[format.Encoding.Count], C = new float[1], V = new float[1] }],
    };
}
