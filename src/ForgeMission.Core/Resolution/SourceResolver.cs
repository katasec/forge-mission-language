using ForgeMission.Core.Parser;

namespace ForgeMission.Core.Resolution;

/// <summary>
/// Resolves expert names from declared sources.
/// Local paths are fully supported. OCI URIs are parsed but blocked (FMS010).
/// </summary>
public class SourceResolver
{
    public Dictionary<string, ResolvedExpert> Resolve(
        IReadOnlyList<UseDeclaration> uses,
        string missionDirectory)
    {
        var catalog = new Dictionary<string, ResolvedExpert>(StringComparer.Ordinal);

        foreach (var use in uses)
        {
            if (use.Source.StartsWith("oci://", StringComparison.OrdinalIgnoreCase))
                throw new MclException(
                    MclErrorCode.OciNotSupported,
                    $"OCI sources are not yet supported: '{use.Source}'",
                    "Remove the OCI use declaration or wait for Phase 11.");

            var sourcePath = Path.IsPathRooted(use.Source)
                ? use.Source
                : Path.GetFullPath(Path.Combine(missionDirectory, use.Source));

            if (!Directory.Exists(sourcePath))
                throw new MclException(
                    MclErrorCode.SourceNotFound,
                    $"Source not found: '{use.Source}'",
                    $"Resolved to: {sourcePath}");

            foreach (var expertDir in Directory.GetDirectories(sourcePath))
            {
                var expertMd = Path.Combine(expertDir, "expert.md");
                if (!File.Exists(expertMd)) continue;

                var name = Path.GetFileName(expertDir);

                // Local sources override remote (and earlier local declarations)
                catalog[name] = new ResolvedExpert(name, use.Source, expertMd);
            }
        }

        return catalog;
    }
}
