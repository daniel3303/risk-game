using Newtonsoft.Json;
using Risk.Sim.AI.Planning;
namespace Risk.Sim.Learning;

/// <summary>
/// An averaged set of value networks used as a correction to the hand-written duel evaluation.
/// Each member's correction is scaled and clamped before averaging; boards outside two-player Classic games are left unchanged.
/// </summary>
public sealed class ValueModel : IBoardValuation
{
    public const int CurrentSchema = 1;
    private static readonly Lazy<ValueModel> ultimate = new(() =>
    {
        using var stream = typeof(ValueModel).Assembly.GetManifestResourceStream("Risk.Sim.Learning.ultimate-model.json")
            ?? throw new InvalidOperationException("The Ultimate value model is missing.");
        return Load(stream);
    });

    [ThreadStatic] private static float[] input;
    [ThreadStatic] private static float[] hidden;

    public int Schema { get; set; } = CurrentSchema;
    public string Features { get; set; } = BoardFeatures.Version;
    public double Scale { get; set; } = 1;
    public double Limit { get; set; } = 60;
    public List<ValueNetwork> Members { get; set; } = [];

    /// <summary>The model played by the Ultimate difficulty.</summary>
    public static ValueModel Ultimate => ultimate.Value;

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
        if (model?.Schema != CurrentSchema || model.Features != BoardFeatures.Version || model.Members == null || model.Members.Count is 0 or > 64
            || model.Members.Any(m => m == null || m.Inputs != BoardFeatures.Count))
            throw new InvalidDataException("Unsupported value model.");
        if (!double.IsFinite(model.Scale) || model.Scale < 0 || !double.IsFinite(model.Limit) || model.Limit < 0)
            throw new InvalidDataException("The value model scale and limit must be finite and nonnegative.");
        foreach (var member in model.Members) member.Unpack();
        return model;
    }

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public double Correction(PositionEvaluator evaluator, PlannerBoard board, double score)
    {
        if (!BoardFeatures.Supports(evaluator)) return 0;
        input ??= new float[BoardFeatures.Count];
        BoardFeatures.Extract(evaluator, board, score, input);
        var total = 0.0;
        foreach (var member in Members)
        {
            if (hidden == null || hidden.Length < member.Hidden) hidden = new float[member.Hidden];
            total += Math.Clamp(Scale * member.Term(input, hidden) * BoardFeatures.ScoreScale / member.A, -Limit, Limit);
        }
        return total / Members.Count;
    }
}
