using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Risk.Learning.Models;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// Self-improvement generations for games with more than two players. Each records league self-play with the current model, fits
/// the next value network bootstrapped from it, then tests the candidate against copies of the current model and against the
/// turtle probe. The candidate is promoted only when it beats the current model with its conservative lower bound above the fair
/// share and the probe is not demonstrably stronger against it than against the current model on the same seeds; a promoted
/// candidate becomes the next generation's learner, and a rejected one ends the run.
/// </summary>
public static class Improvement
{
    /// <param name="ExtraData">Earlier generations' recordings fitted together with this one's, as AlphaZero trains on a window of recent games.</param>
    public sealed record Options(int Players, int FirstSeed, int Games, string Output, ValueModel Current, int Hidden, int Epochs, double Scale, double Limit,
        int EvaluationSeeds, int ProbeSeeds, string PromoteTo, int Threads, int FitSeed = 3, double Lambda = .7, string[] ExtraData = null);

    public sealed record Report(int Players, int FirstSeed, int Games, int Afterstates, string Model, string[] Data, EvaluationSummary AgainstCurrent,
        EvaluationSummary ProbeAgainstCandidate, EvaluationSummary ProbeAgainstCurrent, bool Promoted, string PromotedTo);

    /// <summary>Runs up to <paramref name="generations"/> generations, each from the seeds after the previous one, fitting each on the last <paramref name="window"/> recordings.</summary>
    public static IReadOnlyList<Report> Generations(WorldMap map, Options options, int generations, int window, TextWriter log)
    {
        var reports = new List<Report>();
        var recent = new List<string>(options.ExtraData ?? []);
        for (var generation = 0; generation < generations; generation++)
        {
            var report = Run(map, options with { ExtraData = recent.TakeLast(window - 1).ToArray() }, log);
            reports.Add(report);
            if (!report.Promoted) break;
            recent.Add(report.Data[0]);
            using var promoted = File.OpenRead(report.Model);
            options = options with { FirstSeed = NextSeed(options), Output = NextOutput(options.Output), Current = ValueModel.Load(promoted) };
        }
        return reports;
    }

    /// <summary>A generation plays its recorded games, then its strength seeds, then its probe seeds; the next one starts after them.</summary>
    public static int NextSeed(Options options) => options.FirstSeed + options.Games + options.EvaluationSeeds + options.ProbeSeeds;

    /// <summary>The next generation's directory: a trailing number counts up (gen3, gen4), otherwise -2 is appended.</summary>
    public static string NextOutput(string output)
    {
        var trimmed = output.TrimEnd('/', '\\');
        var start = trimmed.Length;
        while (start > 0 && char.IsAsciiDigit(trimmed[start - 1])) start--;
        if (start == trimmed.Length || start == 0 || trimmed[start - 1] is '/' or '\\') return trimmed + "-2";
        return trimmed[..start] + (int.Parse(trimmed[start..], CultureInfo.InvariantCulture) + 1);
    }

    public static Report Run(WorldMap map, Options options, TextWriter log)
    {
        if (File.Exists(Path.Combine(options.Output, "improve.json")))
            throw new InvalidOperationException($"{options.Output} already holds a finished generation; give the next one a new --output and unused seeds.");
        Directory.CreateDirectory(options.Output);
        var format = TrajectoryFormat.For(options.Players);
        var current = options.Current ?? Empty(format);
        // A resumed run must never read rows of another width, games played by another learner, or seeds the gate will use.
        var data = Path.Combine(options.Output, $"data-{format.Encoding.Version}-{Learner(options.Current)}-{options.FirstSeed}-{options.Games}.bin");
        string[] files = [data, .. options.ExtraData ?? []];
        log.WriteLine($"Generation at {options.Output}: {options.Games} {options.Players}-player games from seed {options.FirstSeed}, learner = {(options.Current == null ? "hand-written terms" : $"model {Learner(options.Current)}")}, fitted on {files.Length} recording(s).");
        TrajectoryRecorder.Record(map, options.Players, options.FirstSeed, options.Games, data, options.Current, options.Threads, log);
        var rows = format.Read(files);
        var network = new ValueTrainer(options.Hidden, options.Epochs, .001, 1e-5, options.Lambda, options.FitSeed, options.Threads, log).Fit(map, rows, options.Current, format);
        var candidate = new ValueModel { Features = format.Encoding.Version, Scale = options.Scale, Limit = options.Limit, Members = [network] };
        var modelPath = Path.Combine(options.Output, "model.json");
        File.WriteAllText(modelPath, candidate.Serialize());
        // Evaluation seeds follow the recorded ones so they were never played before this generation.
        var evaluationSeed = options.FirstSeed + options.Games;
        var probeSeed = evaluationSeed + options.EvaluationSeeds;
        log.WriteLine($"Evaluating the candidate against the current model on seeds {evaluationSeed}+{options.EvaluationSeeds} and against the turtle probe on {probeSeed}+{options.ProbeSeeds}.");
        var against = ModelEvaluator.Run(map, candidate, current, "current", evaluationSeed, options.EvaluationSeeds, options.Players, options.Threads);
        var probeCandidate = ModelEvaluator.Run(map, candidate, "turtle", probeSeed, options.ProbeSeeds, options.Players, true, options.Threads);
        var probeCurrent = ModelEvaluator.Run(map, current, "turtle", probeSeed, options.ProbeSeeds, options.Players, true, options.Threads);
        // The probe check rejects only a demonstrable regression: with 900 games its rate moves several points on noise alone.
        var promoted = against.ConfidenceLower > against.FairShare && probeCandidate.ConfidenceLower <= probeCurrent.WinRate;
        log.WriteLine($"Candidate {against.WinRate:P1} against the current model ({against.ConfidenceLower:P1}-{against.ConfidenceUpper:P1}); turtle {probeCandidate.WinRate:P1} against it, {probeCurrent.WinRate:P1} against the current model; {(promoted ? "promoted" : "rejected")}.");
        string promotedTo = null;
        if (promoted && options.PromoteTo != null)
        {
            File.Copy(modelPath, options.PromoteTo, true);
            promotedTo = options.PromoteTo;
            log.WriteLine($"Copied the candidate to {options.PromoteTo}; rebuild to play it.");
        }
        var report = new Report(options.Players, options.FirstSeed, options.Games, rows.Length, modelPath, files, against, probeCandidate, probeCurrent, promoted, promotedTo);
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
