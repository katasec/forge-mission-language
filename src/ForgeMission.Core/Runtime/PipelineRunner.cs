using System.Text;
using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Parser;

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

            // Track whether any when()-guarded step matched — used for when(else) and error detection.
            var anyGuardedStepMatched = false;
            var hasGuardedSteps       = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is StringEqualsWhen);
            var hasElseBranch         = mission.Pipeline.Elements
                .OfType<StepElement>()
                .Any(e => e.Step.When is ElseWhen);

            foreach (var element in mission.Pipeline.Elements)
            {
                ct.ThrowIfCancellationRequested();

                if (element is ParallelElement parallel)
                {
                    // Phase 21 will make this concurrent (errgroup model).
                    // For now, run sequentially so the grammar works end-to-end.
                    foreach (var step in parallel.Steps)
                    {
                        failReason = await ExecuteStepAsync(step, experts, context, options, ct);
                        if (failReason is not null) break;
                    }
                    if (failReason is not null) break;
                    continue;
                }

                if (element is StepElement se)
                {
                    var step = se.Step;

                    if (step.When is StringEqualsWhen sw2)
                    {
                        var matched = context.TryGetValue(sw2.Key, out var val)
                                      && val?.ToString() == sw2.Value;
                        if (!matched) continue;
                        anyGuardedStepMatched = true;
                    }
                    else if (step.When is ElseWhen)
                    {
                        if (anyGuardedStepMatched) continue;
                    }

                    failReason = await ExecuteStepAsync(step, experts, context, options, ct);
                    if (failReason is not null) break;
                }
            }

            if (failReason is null && hasGuardedSteps && !anyGuardedStepMatched && !hasElseBranch)
                throw new InvalidOperationException(
                    "No when() guard matched and no when(else) branch exists in the pipeline.");

            var text = context.TryGetValue("output", out var last) ? last.ToString()! : string.Empty;

            if (failReason is null)
                return new MissionResult(options.MissionName, text, MissionStatus.Pass, null, attempt);

            lastResult = new MissionResult(options.MissionName, text, MissionStatus.Fail, failReason, attempt);
        }

        return lastResult!;
    }

    private async Task<string?> ExecuteStepAsync(
        Step step,
        Dictionary<string, ExpertDefinition> experts,
        Dictionary<string, object> context,
        PipelineRunOptions options,
        CancellationToken ct)
    {
        if (!experts.TryGetValue(step.ExpertName, out var expert))
            throw new InvalidOperationException(
                $"Expert '{step.ExpertName}' not found. " +
                "Run 'forge validate' to check your mission before running.");

        foreach (var binding in step.Context)
            context[binding.Key] = ContextBuilder.ResolveBindingValue(binding.Value, context);

        // step.Using selects a provider profile — handled by Spoke 5 (provider profiles).

        if (options.StepWriter is { } sw)
            await sw.WriteLineAsync($"→ {step.ExpertName}...");

        StepEnvelope envelope;
        if (options.StepWriter is not null || options.ContentWriter is not null)
        {
            var sb = new StringBuilder();
            await foreach (var chunk in expertRunner.StreamAsync(expert, context, ct))
            {
                if (options.StepWriter is { } sw2)
                    await sw2.WriteAsync(chunk);
                if (options.ContentWriter is { } cw)
                    await cw.WriteAsync(chunk);
                sb.Append(chunk);
            }
            if (options.StepWriter is { } sw3)
                await sw3.WriteLineAsync("\n");
            envelope = ParseStreamedEnvelope(sb.ToString());
        }
        else
        {
            envelope = await expertRunner.RunAsync(expert, context, ct);
        }

        context["output"] = envelope.Text;

        if (envelope.Status == "fail")
            return $"[{step.ExpertName}] {envelope.Reason ?? "step failed"}";

        return null;
    }

    private static StepEnvelope ParseStreamedEnvelope(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize(raw.Trim(), StepEnvelopeContext.Default.StepEnvelope)
                ?? new StepEnvelope(raw);
        }
        catch (JsonException)
        {
            return new StepEnvelope(raw);
        }
    }
}
