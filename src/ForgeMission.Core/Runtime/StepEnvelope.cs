using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Core.Runtime;

public record StepEnvelope(
    [property: JsonPropertyName("text")]   string Text,
    [property: JsonPropertyName("status")] string Status = "pass",
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("meta")]   IReadOnlyDictionary<string, JsonElement>? Meta = null);
