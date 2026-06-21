using System.Runtime.CompilerServices;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ForgeMission.Core.Adapters;

public class OnnxExpertRunner : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var inputs  = expert.Inputs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var features = new float[inputs.Length];

        for (var i = 0; i < inputs.Length; i++)
        {
            var key = inputs[i];
            if (!context.TryGetValue(key, out var raw))
                throw new InvalidOperationException(
                    $"ONNX feature '{key}' not found in context. Ensure a prior step writes it.");
            features[i] = Convert.ToSingle(raw);
        }

        var tensor = new DenseTensor<float>(features, [1, inputs.Length]);
        var ortInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        using var session = new InferenceSession(expert.Model);
        using var results = session.Run(ortInputs);

        var score = results.First().AsEnumerable<float>().First();
        context[expert.OutputKey] = (double)score;

        var threshold = float.Parse(expert.Threshold);
        var status    = score > threshold ? "fail" : "pass";
        var reason    = score > threshold
            ? $"Anomaly score {score:F4} exceeds threshold {threshold}"
            : null;

        return Task.FromResult(new StepEnvelope(score.ToString("F4"), status, reason));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var envelope = await RunAsync(expert, context, ct);
        yield return envelope.Text;
    }
}
