using System.Collections.Concurrent;
using Risk.Sim;
using Risk.Sim.Learning;
using Risk.Sim.Models;
namespace Risk.Learning;

/// <summary>
/// Fits one value network by TD(λ) on recorded afterstates: logit = A·score/50 + B + MLP(features), trained with
/// cross-entropy against λ-returns bootstrapped from a previous model (or from the calibrated hand score).
/// Validation uses every seed divisible by ten.
/// </summary>
public sealed class ValueTrainer(int hidden, int epochs, double rate, double l2, double lambda, int seed, int threads, TextWriter log)
{
    private const int Batch = 512;
    // Gradients are summed over a fixed number of batch slices, so results do not depend on the thread count.
    private const int Slices = 16;
    private const double Beta1 = .9, Beta2 = .999, Epsilon = 1e-8;

    public ValueNetwork Fit(WorldMap map, float[][] rows, ValueModel bootstrap)
    {
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = threads };
        var xs = new float[rows.Length][];
        Parallel.For(0, rows.Length, parallel, r =>
        {
            var (evaluator, board) = TrajectoryFormat.Rebuild(map, rows[r]);
            xs[r] = new float[BoardFeatures.Count];
            BoardFeatures.Extract(evaluator, board, evaluator.Evaluate(board), xs[r]);
        });
        var outcome = rows.Select(r => r[TrajectoryFormat.Outcome]).ToArray();
        var validation = Enumerable.Range(0, rows.Length).Where(r => (int)rows[r][TrajectoryFormat.Seed] % 10 == 0).ToArray();
        var training = Enumerable.Range(0, rows.Length).Where(r => (int)rows[r][TrajectoryFormat.Seed] % 10 != 0).ToArray();
        var random = new Random(seed);
        var network = ValueNetwork.Create(BoardFeatures.Count, hidden, random);
        // Adam moments and the step count persist across every pass of this fit, including the score-only calibration.
        var state = new AdamState(2 + network.Inputs * hidden + hidden * 2);
        // Calibrate the hand score alone first; it is the baseline and, without a previous model, the first bootstrap.
        Train(network, state, xs, outcome, training, 4, .01, 0, true, random, parallel);
        log.WriteLine($"{training.Length} training and {validation.Length} validation afterstates; score-only validation log-loss {LogLoss(network, xs, outcome, validation, true, parallel):F4}");
        var values = new double[rows.Length];
        Parallel.For(0, rows.Length, parallel, r =>
        {
            if (bootstrap == null) values[r] = Sigmoid(network.A * xs[r][BoardFeatures.ScoreIndex] + network.B);
            else values[r] = bootstrap.Members.Average(m => Sigmoid(m.Logit(xs[r], new float[m.Hidden])));
        });
        var target = new float[rows.Length];
        foreach (var sequence in Enumerable.Range(0, rows.Length).GroupBy(r => ((int)rows[r][TrajectoryFormat.Seed], (int)rows[r][TrajectoryFormat.Mover])))
        {
            var ordered = sequence.OrderBy(r => rows[r][TrajectoryFormat.Turn]).ToArray();
            var returns = ValueTargets.LambdaReturns(ordered.Select(r => values[r]).ToArray(), outcome[ordered[^1]], lambda);
            for (var i = 0; i < ordered.Length; i++) target[ordered[i]] = (float)returns[i];
        }
        ValueNetwork best = null;
        var bestLoss = double.MaxValue;
        for (var epoch = 0; epoch < epochs; epoch++)
        {
            var progress = (double)epoch / epochs;
            var step = progress < .5 ? 1 : progress < .75 ? .3 : .1;
            Train(network, state, xs, target, training, 1, rate * step, l2, false, random, parallel);
            var loss = LogLoss(network, xs, outcome, validation, false, parallel);
            log.WriteLine($"epoch {epoch + 1}: validation log-loss {loss:F4} against outcomes, {LogLoss(network, xs, target, validation, false, parallel):F4} against targets");
            if (!(loss < bestLoss)) continue;
            bestLoss = loss;
            best = new ValueNetwork { Inputs = network.Inputs, Hidden = network.Hidden, A = network.A, B = network.B, W = network.W.ToArray(), C = network.C.ToArray(), V = network.V.ToArray() };
        }
        return best ?? throw new InvalidDataException("Training diverged: every validation loss was invalid.");
    }

    private static double Sigmoid(double z) => 1 / (1 + Math.Exp(-z));

    private static double LogLoss(ValueNetwork network, float[][] xs, float[] labels, int[] rows, bool scoreOnly, ParallelOptions parallel)
    {
        var losses = new double[rows.Length];
        Parallel.ForEach(Partitioner.Create(0, rows.Length, 4096), parallel, range =>
        {
            var hidden = new float[network.Hidden];
            for (var i = range.Item1; i < range.Item2; i++)
            {
                var r = rows[i];
                var logit = scoreOnly ? network.A * xs[r][BoardFeatures.ScoreIndex] + network.B : network.Logit(xs[r], hidden);
                var p = Math.Clamp(Sigmoid(logit), 1e-6, 1 - 1e-6);
                losses[i] = -(labels[r] * Math.Log(p) + (1 - labels[r]) * Math.Log(1 - p));
            }
        });
        // Summed in row order so the best-epoch choice is reproducible.
        return losses.Sum() / rows.Length;
    }

    private void Train(ValueNetwork net, AdamState state, float[][] xs, float[] labels, int[] rows, int passes, double learningRate, double decay, bool scoreOnly, Random random, ParallelOptions parallel)
    {
        int n = net.Inputs, hn = net.Hidden;
        var (m, s) = (state.M, state.S);
        var size = m.Length;
        var grads = Enumerable.Range(0, Slices).Select(_ => new double[size]).ToArray();
        var order = rows.ToArray();
        for (var pass = 0; pass < passes; pass++)
        {
            random.Shuffle(order);
            for (var start = 0; start < order.Length; start += Batch)
            {
                var end = Math.Min(order.Length, start + Batch);
                Parallel.For(0, Slices, parallel, t =>
                {
                    var g = grads[t];
                    Array.Clear(g);
                    var h = new float[hn];
                    for (var k = start + t; k < end; k += Slices)
                    {
                        var r = order[k];
                        var x = xs[r];
                        var term = scoreOnly ? 0 : net.Term(x, h);
                        var d = Sigmoid(net.A * x[BoardFeatures.ScoreIndex] + net.B + term) - labels[r];
                        g[0] += d * x[BoardFeatures.ScoreIndex];
                        g[1] += d;
                        if (scoreOnly) continue;
                        int c = 2 + n * hn, v = c + hn;
                        for (var j = 0; j < hn; j++)
                        {
                            if (h[j] <= 0) continue;
                            g[v + j] += d * h[j];
                            g[c + j] += d * net.V[j];
                        }
                        for (var p = 0; p < n; p++)
                        {
                            var xp = x[p];
                            if (xp == 0) continue;
                            var row = 2 + p * hn;
                            for (var j = 0; j < hn; j++) if (h[j] > 0) g[row + j] += d * net.V[j] * xp;
                        }
                    }
                });
                var step = ++state.Step;
                var c1 = 1 - Math.Pow(Beta1, step);
                var c2 = 1 - Math.Pow(Beta2, step);
                var count = end - start;
                var limit = scoreOnly ? 2 : size;
                Parallel.For(0, threads, parallel, t =>
                {
                    for (var p = t; p < limit; p += threads)
                    {
                        var grad = 0.0;
                        for (var k = 0; k < Slices; k++) grad += grads[k][p];
                        grad /= count;
                        var current = Get(net, p);
                        // Biases and the score calibration are not decayed.
                        if (p >= 2 && (p < 2 + n * hn || p >= 2 + n * hn + hn)) grad += decay * current;
                        m[p] = Beta1 * m[p] + (1 - Beta1) * grad;
                        s[p] = Beta2 * s[p] + (1 - Beta2) * grad * grad;
                        Set(net, p, current - learningRate * (m[p] / c1) / (Math.Sqrt(s[p] / c2) + Epsilon));
                    }
                });
            }
        }
    }

    private static double Get(ValueNetwork net, int p)
    {
        int wEnd = 2 + net.Inputs * net.Hidden, cEnd = wEnd + net.Hidden;
        return p == 0 ? net.A : p == 1 ? net.B : p < wEnd ? net.W[p - 2] : p < cEnd ? net.C[p - wEnd] : net.V[p - cEnd];
    }

    private static void Set(ValueNetwork net, int p, double value)
    {
        int wEnd = 2 + net.Inputs * net.Hidden, cEnd = wEnd + net.Hidden;
        if (p == 0) net.A = (float)value;
        else if (p == 1) net.B = (float)value;
        else if (p < wEnd) net.W[p - 2] = (float)value;
        else if (p < cEnd) net.C[p - wEnd] = (float)value;
        else net.V[p - cEnd] = (float)value;
    }
}
