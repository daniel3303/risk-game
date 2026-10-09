using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Risk.Learning;
using Risk.Learning.Models;
using Risk.Sim;
using Risk.Sim.AI;
using Risk.Sim.Learning;
using Risk.Sim.Models;

const string Usage = """
    Risk value learning (Classic, fixed cards, automatic setup; --players 2 trains the duel model, 3-6 the multiplayer model):
      record   --output traj.bin --first-seed 760000 --games 30000 [--players 2] [--model model.json] [--scale 0.5] [--threads 8]
      fit      --data a.bin,b.bin --output net.json [--players 2] [--bootstrap model.json] [--hidden 64] [--epochs 8] [--rate 0.001] [--l2 0.00001] [--lambda 0.7] [--seed 3] [--threads 8]
      bundle   --members a.json,b.json --output model.json [--scale 0.5] [--limit 60]
      evaluate --model model.json --first-seed 900000 --seeds 2000 [--players 2] [--opponent master|turtle|…] [--defend false] [--scale s] [--threads 8]
      improve  --output artifacts/learning/gen1 --first-seed 1000000 --games 20000 [--players 3] [--model current.json] [--hidden 64] [--epochs 8] [--scale 0.5] [--limit 60]
               [--evaluation-seeds 1000] [--probe-seeds 300] [--promote src/Risk.Sim/Learning/ultimate-multiplayer-model.json] [--threads 8]
      luck     --first-seed 870000 [--deals 200] [--replays 20] [--threads 8]
    """;
// Logs and parsed numbers use invariant formatting regardless of the machine's locale.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine(Usage);
    return 0;
}
try
{
    using var stream = typeof(TrajectoryRecorder).Assembly.GetManifestResourceStream("Risk.Learning.classic.json");
    using var reader = new StreamReader(stream);
    var map = WorldMap.Parse(reader.ReadToEnd());
    var options = CommandOptions.Parse(args[1..]);
    var threads = options.Integer("--threads", Environment.ProcessorCount, 1, 64);
    var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver(), Formatting = Formatting.Indented };
    switch (args[0])
    {
        case "record":
        {
            var output = options.Text("--output");
            var players = options.Integer("--players", 2, 2, 6);
            var model = LoadModel(options.OptionalText("--model"), options, players);
            var first = options.Integer("--first-seed", 0, 0, TrajectoryFormat.SeedLimit - 1);
            var games = options.Integer("--games", 1000, 1, TrajectoryFormat.SeedLimit - first);
            options.RejectUnknown();
            TrajectoryRecorder.Record(map, players, first, games, output, model, threads, Console.Out);
            break;
        }
        case "fit":
        {
            var data = options.Text("--data").Split(',');
            var output = options.Text("--output");
            var players = options.Integer("--players", 2, 2, 6);
            var format = TrajectoryFormat.For(players);
            var bootstrap = LoadModel(options.OptionalText("--bootstrap"), null, players);
            var trainer = new ValueTrainer(options.Integer("--hidden", 64, 1, 1024), options.Integer("--epochs", 8, 1, 1000),
                options.Number("--rate", .001, 1e-6, 1), options.Number("--l2", 1e-5, 0, 1), options.Number("--lambda", .7, 0, 1),
                options.Integer("--seed", 3, 0, int.MaxValue), threads, Console.Out);
            options.RejectUnknown();
            var network = trainer.Fit(map, format.Read(data), bootstrap, format);
            File.WriteAllText(output, new ValueModel { Features = format.Encoding.Version, Members = [network] }.Serialize());
            break;
        }
        case "bundle":
        {
            var models = options.Text("--members").Split(',').Select(path => LoadModel(path, null, null)).ToList();
            if (models.Select(m => m.Features).Distinct().Count() != 1) throw new ArgumentException("Bundle members must share one board encoding.");
            var output = options.Text("--output");
            var model = new ValueModel { Features = models[0].Features, Scale = options.Number("--scale", .5, 0, 10), Limit = options.Number("--limit", 60, 0, 1000), Members = models.SelectMany(m => m.Members).ToList() };
            options.RejectUnknown();
            File.WriteAllText(output, model.Serialize());
            break;
        }
        case "evaluate":
        {
            var players = options.Integer("--players", 2, 2, 6);
            var model = LoadModel(options.Text("--model"), options, players);
            var opponent = options.Text("--opponent", players == 2 ? "master" : "ultimate").ToLowerInvariant();
            if (!PolicyCatalog.IsKnown(opponent)) throw new ArgumentException($"Unknown opponent {opponent}; choose one of {string.Join(", ", PolicyCatalog.Names)}.");
            var defend = Flag(options, "--defend");
            var first = options.Integer("--first-seed", 0, 0, int.MaxValue - 5000);
            var seeds = options.Integer("--seeds", 500, 1, 5000);
            options.RejectUnknown();
            Console.WriteLine(JsonConvert.SerializeObject(ModelEvaluator.Run(map, model, opponent, first, seeds, players, defend, threads), json));
            break;
        }
        case "improve":
        {
            var players = options.Integer("--players", 3, 3, 6);
            var current = LoadModel(options.OptionalText("--model"), null, players) ?? ValueModel.UltimateMultiplayer;
            var first = options.Integer("--first-seed", 1000000, 0, TrajectoryFormat.SeedLimit - 1);
            var games = options.Integer("--games", 20000, 1, TrajectoryFormat.SeedLimit - first);
            var improvement = new Improvement.Options(players, first, games, options.Text("--output"), current,
                options.Integer("--hidden", 64, 1, 1024), options.Integer("--epochs", 8, 1, 1000), options.Number("--scale", .5, 0, 10), options.Number("--limit", 60, 0, 1000),
                options.Integer("--evaluation-seeds", 1000, 1, 5000), options.Integer("--probe-seeds", 300, 1, 5000), options.OptionalText("--promote"), threads);
            options.RejectUnknown();
            var report = Improvement.Run(map, improvement, Console.Out);
            Console.WriteLine(JsonConvert.SerializeObject(report, json));
            break;
        }
        case "luck":
        {
            var first = options.Integer("--first-seed", 0, 0, int.MaxValue - 100_000);
            var deals = options.Integer("--deals", 200, 1, 100_000);
            var replays = options.Integer("--replays", 20, 2, 1000);
            options.RejectUnknown();
            Console.WriteLine(JsonConvert.SerializeObject(LuckAnalysis.Run(map, first, deals, replays, threads), json));
            break;
        }
        default:
            throw new ArgumentException($"Unknown command {args[0]}.");
    }
    return 0;
}
catch (Exception error) when (error is ArgumentException or FormatException or OverflowException or InvalidDataException or IOException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

// Loads a model file; "--scale" (when the command passes its options) overrides the model's own scale, and the encoding must suit the player count.
static ValueModel LoadModel(string path, CommandOptions options, int? players)
{
    if (path == null) return null;
    using var stream = File.OpenRead(path);
    var model = ValueModel.Load(stream);
    if (options != null) model.Scale = options.Number("--scale", model.Scale, 0, 10);
    if (players is int count && model.Encoding != TrajectoryFormat.For(count).Encoding)
        throw new ArgumentException($"{path} is a {model.Features} model and cannot be used in {count}-player games.");
    return model;
}

static bool Flag(CommandOptions options, string name) => options.Text(name, "false").ToLowerInvariant() switch
{
    "true" => true, "false" => false, _ => throw new ArgumentException($"{name} must be true or false."),
};
