using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

public class SourceResolverTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public SourceResolverTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteExpert(string name)
    {
        var expertDir = Path.Combine(_dir, SourceResolver.DefaultExpertsDir, name);
        Directory.CreateDirectory(expertDir);
        File.WriteAllText(Path.Combine(expertDir, "expert.md"), $"""
            ---
            name: {name}
            input: Input
            output: Output
            ---
            You are {name}.
            """);
    }

    [Fact]
    public void Resolve_FindsExpertsInDefaultDir()
    {
        WriteExpert("KubernetesArchitect");
        WriteExpert("SecurityArchitect");

        var catalog = new SourceResolver().Resolve(_dir);

        Assert.Equal(2, catalog.Count);
        Assert.True(catalog.ContainsKey("KubernetesArchitect"));
        Assert.True(catalog.ContainsKey("SecurityArchitect"));
    }

    [Fact]
    public void Resolve_MissingExpertsDir_ThrowsFms005()
    {
        var ex = Assert.Throws<MclException>(() =>
            new SourceResolver().Resolve(_dir));

        Assert.Equal(MclErrorCode.SourceNotFound, ex.Code);
    }

    [Fact]
    public void Resolve_ResolvedExpert_PathExists()
    {
        WriteExpert("KubernetesArchitect");

        var catalog = new SourceResolver().Resolve(_dir);

        Assert.True(File.Exists(catalog["KubernetesArchitect"].ExpertMdPath));
    }
}
