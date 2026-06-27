using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Core.Adapters;

public class JsonExtractExpertRunner : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var raw  = context.TryGetValue("output", out var o) ? o?.ToString() ?? "" : "";
        var json = ExtractJson(raw, out var prose);

        JsonDocument doc;
        try   { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            var hint = prose is null
                ? "output contains neither valid JSON nor a ```json fence"
                : "content inside ```json fence is not valid JSON";
            throw new InvalidOperationException($"json_extract ({expert.Name}): {hint}", ex);
        }

        using (doc)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                context[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number                         => (object)prop.Value.GetDouble(),
                    JsonValueKind.Array or JsonValueKind.Object  => prop.Value.GetRawText(),
                    _                                            => prop.Value.ToString(),
                };
            }
        }

        // Prose (from mixed output) replaces context["output"] so the next LLM step
        // sees the reasoning narrative rather than the raw JSON block.
        if (prose is not null)
            context["output"] = prose;

        return Task.FromResult(new StepEnvelope(json));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var envelope = await RunAsync(expert, context, ct);
        yield return envelope.Text;
    }

    // Extracts the first ```json ... ``` block from mixed prose+JSON output.
    // Returns the JSON string and sets prose to the text outside the block.
    // If no fence is found, returns the raw string as-is (pure JSON path).
    private static string ExtractJson(string raw, out string? prose)
    {
        const string openFence  = "```json";
        const string closeFence = "```";

        var start = raw.IndexOf(openFence, StringComparison.OrdinalIgnoreCase);
        if (start < 0) { prose = null; return raw; }

        var contentStart = start + openFence.Length;
        var end          = raw.IndexOf(closeFence, contentStart, StringComparison.Ordinal);
        if (end < 0)    { prose = null; return raw; }

        var json        = raw[contentStart..end].Trim();
        var beforeFence = raw[..start].Trim();
        var afterFence  = raw[(end + closeFence.Length)..].Trim();
        prose = string.IsNullOrWhiteSpace(afterFence)
            ? beforeFence
            : string.IsNullOrWhiteSpace(beforeFence) ? afterFence : $"{beforeFence}\n{afterFence}";

        return json;
    }
}
