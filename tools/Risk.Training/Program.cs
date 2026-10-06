using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Risk.Sim.Learning;
using Risk.Sim.Models;
using Risk.Training;
using Risk.Training.Models;

using var stream = typeof(TrainingSession).Assembly.GetManifestResourceStream("Risk.Training.classic.json");
using var reader = new StreamReader(stream);
var session = new TrainingSession(WorldMap.Parse(reader.ReadToEnd()));
var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
Console.WriteLine(JsonConvert.SerializeObject(new { version = ObservationEncoder.Version, stateSize = ObservationEncoder.StateSize,
    candidateSize = ObservationEncoder.CandidateSize, capacity = ActionCatalog.Capacity }, settings));
string line;
while ((line = Console.ReadLine()) != null)
{
    try
    {
        var request = JsonConvert.DeserializeObject<TrainingRequest>(line) ?? throw new ArgumentException("A request is required.");
        Console.WriteLine(JsonConvert.SerializeObject(session.Apply(request), settings));
    }
    catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException or Risk.Sim.RuleException)
    {
        Console.WriteLine(JsonConvert.SerializeObject(new { error = error.Message }, settings));
    }
}
