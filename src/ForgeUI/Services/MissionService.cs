using System.Text;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeUI.Models;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeUI.Services;

public class MissionService(
    MclProgram                       ast,
    Dictionary<string, ExpertDefinition> experts,
    IExpertRunner                    runner)
{
    public async Task<ChatMessage> RunAsync(
        string                        userText,
        Action<PipelineTraceEvent>    onStep,
        CancellationToken             ct = default)
    {
        var trace      = new List<PipelineTraceEvent>();
        var stepWriter = new TraceWriter(ast, experts, runner, trace, onStep);

        var mission   = ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = mission.Params.FirstOrDefault() ?? "goal";
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = userText };
        var options   = new PipelineRunOptions(mission.Name, vars);

        var result = await new PipelineRunner(runner).RunAsync(ast, experts, options, ct);

        var trust = new TrustSignal(
            Verified:   result.Status == MissionStatus.Pass,
            StepCount:  trace.Count,
            RetryCount: result.Attempts - 1
        );

        return new ChatMessage(userText, result.Text, trust, trace);
    }

    // Captures per-step output by intercepting ContentWriter chunks per expert.
    // Since PipelineRunner runs experts sequentially, we hook via a custom TextWriter
    // that flushes a PipelineTraceEvent after each expert completes.
    private sealed class TraceWriter(
        MclProgram                       ast,
        Dictionary<string, ExpertDefinition> experts,
        IExpertRunner                    runner,
        List<PipelineTraceEvent>         trace,
        Action<PipelineTraceEvent>       onStep) : TextWriter
    {
        private readonly StringBuilder _buffer = new();
        private int _attempt = 1;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)       => _buffer.Append(value);
        public override Task WriteAsync(string? value)
        {
            if (!string.IsNullOrEmpty(value)) _buffer.Append(value);
            return Task.CompletedTask;
        }

        public void Flush(string expertName, string status, string? reason)
        {
            var text     = _buffer.ToString();
            _buffer.Clear();
            var envelope = new StepEnvelope(text, status, reason);
            var ev       = new PipelineTraceEvent(expertName, envelope, DateTime.UtcNow, _attempt);
            trace.Add(ev);
            onStep(ev);
            if (status == "fail") _attempt++;
        }
    }
}
