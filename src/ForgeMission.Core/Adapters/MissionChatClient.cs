using Microsoft.Extensions.AI;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Runtime;
using MclProgram = ForgeMission.Core.Parser.Program;

namespace ForgeMission.Core.Adapters;

// Wraps PipelineRunner as an IChatClient so OaiServer has no knowledge of MCL.
// The last user message in the conversation maps to the mission's `goal` parameter.
public sealed class MissionChatClient(
    MclProgram ast,
    Dictionary<string, ExpertDefinition> experts,
    IExpertRunner runner) : IChatClient
{
    public ChatClientMetadata Metadata => new("forge-mission", null, null);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var goal       = LastUserMessage(messages);
        var runOptions = BuildOptions(goal, stepWriter: null);
        var result     = await new PipelineRunner(runner).RunAsync(ast, experts, runOptions, ct);

        if (result.Status == MissionStatus.Fail)
            throw new InvalidOperationException($"Mission failed: {result.FailReason}");

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, result.Text)]);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Run the full pipeline; stream the final output as a single chunk.
        // Per-token streaming of the last expert is a future iteration.
        var response = await GetResponseAsync(messages, options, ct);
        var text     = response.Messages.FirstOrDefault()?.Text ?? string.Empty;
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? key = null) => null;

    // -------------------------------------------------------------------------

    private PipelineRunOptions BuildOptions(string goal, TextWriter? stepWriter)
    {
        var mission = ast.Declarations.OfType<MissionDeclaration>().First();
        var vars    = new Dictionary<string, string>(StringComparer.Ordinal) { ["goal"] = goal };
        return new PipelineRunOptions(mission.Name, vars, stepWriter);
    }

    private static string LastUserMessage(IEnumerable<ChatMessage> messages)
        => messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
}
