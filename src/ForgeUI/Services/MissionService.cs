using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeUI.Models;

namespace ForgeUI.Services;

public class MissionService(MissionRegistry registry)
{
    public async Task<ChatMessage> RunAsync(
        string                     userText,
        MissionEntry               mission,
        Action<PipelineTraceEvent> onStep,
        CancellationToken          ct = default)
    {
        var trace   = new List<PipelineTraceEvent>();
        var attempt = 1;

        var decl      = mission.Ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = decl.Params.FirstOrDefault() ?? "goal";
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = userText };

        var options = new PipelineRunOptions(
            decl.Name,
            vars,
            OnStepComplete: (expertName, envelope) =>
            {
                if (envelope.Status == "fail") attempt++;
                var ev = new PipelineTraceEvent(expertName, envelope, DateTime.UtcNow, attempt);
                trace.Add(ev);
                onStep(ev);
            });

        var result = await new PipelineRunner(mission.Runner).RunAsync(mission.Ast, mission.Experts, options, ct);

        var verified = result.Status == MissionStatus.Pass;

        string displayText;
        if (verified)
        {
            displayText = trace.LastOrDefault(e => e.ExpertName == "Answerer")?.Envelope.Text
                          ?? result.Text;
        }
        else
        {
            var lastFailReason = trace.LastOrDefault(e => e.Envelope.Status == "fail")?.Envelope.Reason;
            displayText = string.IsNullOrWhiteSpace(lastFailReason)
                ? "Could not verify this answer after multiple attempts."
                : $"Could not verify: {lastFailReason}";
        }

        var trust = new TrustSignal(
            Verified:   verified,
            StepCount:  trace.Count,
            RetryCount: result.Attempts - 1);

        return new ChatMessage(userText, displayText, trust, trace, mission.Label);
    }
}
