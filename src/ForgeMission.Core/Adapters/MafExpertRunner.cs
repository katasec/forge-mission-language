using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

/// <summary>
/// IExpertRunner implementation backed by Microsoft Agent Framework.
/// This is the only file in the codebase that touches MAF.
/// </summary>
public class MafExpertRunner(IChatClient chatClient) : IExpertRunner
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var userMessage = context.TryGetValue("output", out var output) && !string.IsNullOrWhiteSpace(output?.ToString())
            ? output.ToString()!
            : "Begin.";

        var systemPrompt = ContextInterpolator.Interpolate(expert.SystemPrompt, context);

        var agent    = new ChatClientAgent(chatClient, systemPrompt, expert.Name);
        var session  = await agent.CreateSessionAsync(ct);
        var response = await agent.RunAsync<StepEnvelope>(userMessage, session, _jsonOptions, new ChatClientAgentRunOptions(), ct);
        return response.Result;
    }
}
