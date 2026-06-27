using System.Text.RegularExpressions;

namespace ForgeMission.Core.Runtime;

public static partial class ContextInterpolator
{
    // [\w.]+ allows dotted keys such as {{Optimist.output}} set by parallel blocks.
    [GeneratedRegex(@"\{\{([\w.]+)\}\}")]
    private static partial Regex PlaceholderPattern();

    public static string Interpolate(string template, Dictionary<string, object> context)
        => PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return context.TryGetValue(key, out var value)
                ? value?.ToString() ?? ""
                : match.Value; // leave placeholder intact when key is absent
        });
}
