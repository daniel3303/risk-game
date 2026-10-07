using System.Numerics;
using Newtonsoft.Json;
namespace Risk.Sim.Learning;

/// <summary>
/// One-hidden-layer win-probability model: logit = A·x[score] + B + V·relu(W·x + C).
/// The learned term V·relu(W·x + C), divided by A, is a correction in hand-written score units.
/// </summary>
public sealed class ValueNetwork
{
    public int Inputs { get; set; }
    public int Hidden { get; set; }
    public float A { get; set; }
    public float B { get; set; }
    /// <summary>Input-major hidden weights: W[input * Hidden + unit].</summary>
    [JsonIgnore] public float[] W { get; set; } = [];
    [JsonIgnore] public float[] C { get; set; } = [];
    [JsonIgnore] public float[] V { get; set; } = [];

    /// <summary>W, C and V as little-endian 32-bit floats, stored as base64 in JSON.</summary>
    public byte[] Parameters
    {
        get
        {
            var values = W.Concat(C).Concat(V).ToArray();
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        set
        {
            var values = new float[value.Length / sizeof(float)];
            Buffer.BlockCopy(value, 0, values, 0, values.Length * sizeof(float));
            if (values.Length != Inputs * Hidden + Hidden * 2) throw new InvalidDataException("The value network parameters do not match its size.");
            W = values[..(Inputs * Hidden)];
            C = values[(Inputs * Hidden)..(Inputs * Hidden + Hidden)];
            V = values[(Inputs * Hidden + Hidden)..];
        }
    }

    public static ValueNetwork Create(int inputs, int hidden, Random random)
    {
        var network = new ValueNetwork { Inputs = inputs, Hidden = hidden, A = 1, W = new float[inputs * hidden], C = new float[hidden], V = new float[hidden] };
        // About forty inputs are active in a typical position.
        var scale = Math.Sqrt(2.0 / 40);
        for (var i = 0; i < network.W.Length; i++) network.W[i] = (float)((random.NextDouble() * 2 - 1) * scale);
        for (var j = 0; j < hidden; j++) network.V[j] = (float)((random.NextDouble() * 2 - 1) * .01);
        return network;
    }

    /// <summary>
    /// The learned logit term; <paramref name="hidden"/> receives the hidden activations. Vector lanes span hidden units, so every
    /// unit still adds its inputs one at a time in input order and results do not depend on the platform's vector width.
    /// </summary>
    public double Term(float[] x, float[] hidden)
    {
        Array.Copy(C, hidden, Hidden);
        var width = Vector<float>.Count;
        for (var i = 0; i < Inputs; i++)
        {
            var value = x[i];
            if (value == 0) continue;
            var row = W.AsSpan(i * Hidden, Hidden);
            var j = 0;
            if (Vector.IsHardwareAccelerated)
            {
                var factor = new Vector<float>(value);
                for (; j <= Hidden - width; j += width)
                    (new Vector<float>(hidden, j) + new Vector<float>(row[j..]) * factor).CopyTo(hidden, j);
            }
            for (; j < Hidden; j++) hidden[j] += row[j] * value;
        }
        var sum = 0.0;
        for (var j = 0; j < Hidden; j++)
        {
            if (hidden[j] < 0) hidden[j] = 0;
            sum += (double)hidden[j] * V[j];
        }
        return sum;
    }

    public double Logit(float[] x, float[] hidden) => A * x[BoardFeatures.ScoreIndex] + B + Term(x, hidden);
}
