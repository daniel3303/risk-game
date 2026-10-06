using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Risk.Arena;
using Risk.Arena.Models;
using Risk.Sim.Models;

if (args.Contains("--help"))
{
    Console.WriteLine("Risk Arena: --seeds 16 --first-seed 1000 --players 2 --candidate expert --opponent hard --cards fixed --setup automatic --max-rounds 200 --max-actions 20000");
    return 0;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
try
{
    var options = ArenaOptions.Parse(args);
    using var stream = typeof(ArenaRunner).Assembly.GetManifestResourceStream("Risk.Arena.classic.json");
    using var reader = new StreamReader(stream);
    var report = ArenaRunner.Run(WorldMap.Parse(reader.ReadToEnd()), options, cancellation.Token);
    Console.WriteLine(JsonConvert.SerializeObject(report, Formatting.Indented, new JsonSerializerSettings {
        ContractResolver = new CamelCasePropertyNamesContractResolver(), Converters = [new StringEnumConverter(new CamelCaseNamingStrategy())]
    }));
    return 0;
}
catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Evaluation cancelled.");
    return 130;
}
