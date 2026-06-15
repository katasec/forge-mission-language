namespace ForgeMission.Core.Resolution;

/// <summary>
/// Discovers experts from the conventional ./experts directory.
/// </summary>
public class SourceResolver
{
    public const string DefaultExpertsDir = "experts";

    public Dictionary<string, ResolvedExpert> Resolve(string missionDirectory)
    {
        var sourcePath = Path.GetFullPath(Path.Combine(missionDirectory, DefaultExpertsDir));

        if (!Directory.Exists(sourcePath))
            throw new MclException(
                MclErrorCode.SourceNotFound,
                $"Experts directory not found: '{DefaultExpertsDir}'",
                $"Resolved to: {sourcePath}. Create the directory and add expert subdirectories.");

        var catalog = new Dictionary<string, ResolvedExpert>(StringComparer.Ordinal);

        foreach (var expertDir in Directory.GetDirectories(sourcePath))
        {
            var expertMd = Path.Combine(expertDir, "expert.md");
            if (!File.Exists(expertMd)) continue;

            var name = Path.GetFileName(expertDir);
            catalog[name] = new ResolvedExpert(name, DefaultExpertsDir, expertMd);
        }

        return catalog;
    }
}
