using Newtonsoft.Json;
using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>
/// An averaged set of value networks used as a correction to the hand-written evaluation.
/// Each member's correction is scaled and clamped before averaging; boards outside the model's encoding are left unchanged.
/// </summary>
public sealed class ValueModel : IBoardValuation
{
    public const int CurrentSchema = 1;
    private static readonly Lazy<ValueModel> ultimate = new(() => Embedded("ultimate-model.json") ?? throw new InvalidOperationException("The Ultimate value model is missing."));
    private static readonly Lazy<ValueModel> ultimateMultiplayer = new(() => Embedded("ultimate-multiplayer-model.json"));

    [ThreadStatic] private static float[] input;
    [ThreadStatic] private static float[] hidden;
    private BoardEncoding encoding;

    public int Schema { get; set; } = CurrentSchema;
    public string Features { get; set; } = BoardEncoding.Duel.Version;
    public double Scale { get; set; } = 1;
    public double Limit { get; set; } = 60;
    public List<ValueNetwork> Members { get; set; } = [];

    /// <summary>The encoding named by <see cref="Features"/>.</summary>
    [JsonIgnore] public BoardEncoding Encoding => encoding ??= BoardEncoding.For(Features) ?? throw new InvalidDataException("Unsupported value model.");

    /// <summary>The duel model played by the Ultimate difficulty.</summary>
    public static ValueModel Ultimate => ultimate.Value;
    /// <summary>The model Ultimate plays while more than two players remain, or null when no such model is bundled.</summary>
    public static ValueModel UltimateMultiplayer => ultimateMultiplayer.Value;

    private static ValueModel Embedded(string name)
    {
        using var stream = typeof(ValueModel).Assembly.GetManifestResourceStream($"Risk.Sim.Learning.{name}");
        return stream == null ? null : Load(stream);
    }

    public static ValueModel Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        ValueModel model;
        try
        {
            model = JsonConvert.DeserializeObject<ValueModel>(reader.ReadToEnd());
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The value model is not valid JSON.", error);
        }
        var encoding = model?.Features == null ? null : BoardEncoding.For(model.Features);
        if (model?.Schema != CurrentSchema || encoding == null || model.Members == null || model.Members.Count is 0 or > 64
            || model.Members.Any(m => m == null || m.Inputs != encoding.Count))
            throw new InvalidDataException("Unsupported value model.");
        if (!double.IsFinite(model.Scale) || model.Scale < 0 || !double.IsFinite(model.Limit) || model.Limit < 0)
            throw new InvalidDataException("The value model scale and limit must be finite and nonnegative.");
        foreach (var member in model.Members) member.Unpack();
        return model;
    }

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public double Correction(PositionEvaluator evaluator, PlannerBoard board, double score)
    {
        if (!Encoding.Supports(evaluator)) return 0;
        // The buffers are shared by every model on the thread; an encoding only reads its own first Count entries.
        if (input == null || input.Length < Encoding.Count) input = new float[Encoding.Count];
        Encoding.Extract(evaluator, board, score, input);
        var total = 0.0;
        foreach (var member in Members)
        {
            if (hidden == null || hidden.Length < member.Hidden) hidden = new float[member.Hidden];
            total += Math.Clamp(Scale * member.Term(input, hidden) * BoardEncoding.ScoreScale / member.A, -Limit, Limit);
        }
        return total / Members.Count;
    }
}
