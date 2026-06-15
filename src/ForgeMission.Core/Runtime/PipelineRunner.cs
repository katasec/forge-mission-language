using System.Text;
using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;

namespace ForgeMission.Core.Runtime;

public class PipelineRunner(IExpertRunner expertRunner)
{
    public async Task<MissionResult> RunAsync(
        Program ast,
        Dictionary<string, ExpertDefinition> experts,
        PipelineRunOptions options,
        CancellationToken ct = default)
    {
        var mission = ast.Declarations
            .OfType<MissionDeclaration>()
            .FirstOrDefault(m => m.Name == options.MissionName)
            ?? throw new InvalidOperationException(
                $"Mission '{options.MissionName}' not found in .mcl file");

        var maxLoops = mission.MaxLoops;
        var steps    = Flatten(mission.Pipeline, ast);

        MissionResult? lastResult = null;

        for (var attempt = 1; attempt <= maxLoops; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (options.StepWriter is { } sw && maxLoops > 1)
                await sw.WriteLineAsync($"(attempt {attempt}/{maxLoops})");

            var context = ContextBuilder.Seed(ast, options.Vars);
            context["attempt"]   = attempt.ToString();
            context["max_loops"] = maxLoops.ToString();

            string? failReason = null;

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();

                if (!experts.TryGetValue(step.ExpertName, out var expert))
                    throw new InvalidOperationException(
                        $"Expert '{step.ExpertName}' not found. " +
                        "Run 'mcl validate' to check your mission before running.");

                foreach (var binding in step.With)
                    context[binding.Key] = ContextBuilder.ResolveBindingValue(binding.Value, context);

                if (options.StepWriter is { } sw2)
                    await sw2.WriteLineAsync($"→ {step.ExpertName}...");

                StepEnvelope envelope;
                if (options.StepWriter is { } sw3)
                {
                    var sb = new StringBuilder();
                    await foreach (var chunk in expertRunner.StreamAsync(expert, context, ct))
                    {
                        await sw3.WriteAsync(chunk);
                        sb.Append(chunk);
                    }
                    await sw3.WriteLineAsync("\n");
                    envelope = ParseStreamedEnvelope(sb.ToString());
                }
                else
                {
                    envelope = await expertRunner.RunAsync(expert, context, ct);
                }

                context["output"] = envelope.Text;

                if (envelope.Status == "fail")
                {
                    failReason = $"[{step.ExpertName}] {envelope.Reason ?? "step failed"}";
                    break;
                }
            }

            var text = context.TryGetValue("output", out var last) ? last.ToString()! : string.Empty;

            if (failReason is null)
                return new MissionResult(options.MissionName, text, MissionStatus.Pass, null, attempt);

            lastResult = new MissionResult(options.MissionName, text, MissionStatus.Fail, failReason, attempt);
        }

        return lastResult!;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static StepEnvelope ParseStreamedEnvelope(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<StepEnvelope>(raw.Trim(), _jsonOptions)
                ?? new StepEnvelope(raw);
        }
        catch (JsonException)
        {
            return new StepEnvelope(raw);
        }
    }

    private static List<Step> Flatten(Pipeline pipeline, Program ast)
    {
        var expertDecls = ast.Declarations
            .OfType<ExpertDeclaration>()
            .ToDictionary(e => e.Name, StringComparer.Ordinal);

        var result = new List<Step>();
        foreach (var step in pipeline.Steps)
            FlattenStep(step, expertDecls, result, []);

        return result;
    }

    private static void FlattenStep(
        Step step,
        Dictionary<string, ExpertDeclaration> decls,
        List<Step> result,
        HashSet<string> visited)
    {
        if (!visited.Add(step.ExpertName))
            throw new InvalidOperationException(
                $"Circular expert reference detected: '{step.ExpertName}'");

        if (decls.TryGetValue(step.ExpertName, out var decl))
        {
            foreach (var inner in decl.Pipeline.Steps)
                FlattenStep(inner, decls, result, new HashSet<string>(visited, StringComparer.Ordinal));
        }
        else
        {
            result.Add(step);
        }
    }
}
