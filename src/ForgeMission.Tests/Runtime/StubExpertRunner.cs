using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Tests.Runtime;

public class StubExpertRunner : IExpertRunner
{
    private readonly Func<string, Dictionary<string, object>, StepEnvelope> _respond;

    public StubExpertRunner(Func<string, Dictionary<string, object>, string>? respond = null)
    {
        var fn = respond ?? ((name, _) => $"Output from {name}");
        _respond = (name, ctx) => new StepEnvelope(fn(name, ctx));
    }

    public StubExpertRunner(Func<string, Dictionary<string, object>, StepEnvelope> respond)
    {
        _respond = respond;
    }

    public List<(string ExpertName, Dictionary<string, object> Context)> Calls { get; } = [];

    public Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((expert.Name, new Dictionary<string, object>(context, StringComparer.Ordinal)));
        return Task.FromResult(_respond(expert.Name, context));
    }
}
