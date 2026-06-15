namespace ForgeMission.Core.Resolution;

/// <summary>
/// Resolves paths inside the global forge cache (~/.forge).
/// Uses Environment.SpecialFolder.UserProfile for cross-platform home directory resolution.
/// </summary>
public static class ForgeCache
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".forge");

    /// <summary>
    /// Returns the absolute path where an OCI expert's expert.md should be cached.
    /// Layout: ~/.forge/experts/{registry}/{name}/{version}/expert.md
    /// </summary>
    public static string ExpertsRoot => Path.Combine(Root, "experts");

    public static string ExpertMdPath(string registry, string ociName, string version)
        => Path.Combine(Root, "experts", registry, ociName, version, "expert.md");
}
