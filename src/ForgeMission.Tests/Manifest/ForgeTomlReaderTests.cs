using ForgeMission.Core.Manifest;

namespace ForgeMission.Tests.Manifest;

public class ForgeTomlReaderTests
{
    // Write a forge.toml to a temp dir next to a fake mission.mcl and return the mission path.
    private static string WriteTempManifest(string toml)
    {
        var dir         = Directory.CreateTempSubdirectory("forge-toml-test-").FullName;
        var missionPath = Path.Combine(dir, "mission.mcl");
        var tomlPath    = Path.Combine(dir, "forge.toml");
        File.WriteAllText(missionPath, "");
        File.WriteAllText(tomlPath, toml);
        return missionPath;
    }

    [Fact]
    public void NoForgeToml_ReturnsNull()
    {
        var dir         = Directory.CreateTempSubdirectory("forge-toml-missing-").FullName;
        var missionPath = Path.Combine(dir, "mission.mcl");
        File.WriteAllText(missionPath, "");

        var result = ForgeTomlReader.TryRead(missionPath);

        Assert.Null(result);
    }

    [Fact]
    public void ExpertsSection_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [experts]
            KubernetesArchitect = "ghcr.io/katasec/forge-k8s-architect@0.1.0"
            SecurityArchitect   = "ghcr.io/katasec/forge-security-architect@0.1.0"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.NotNull(manifest);
        Assert.Equal("ghcr.io/katasec/forge-k8s-architect@0.1.0",      manifest.Experts["KubernetesArchitect"]);
        Assert.Equal("ghcr.io/katasec/forge-security-architect@0.1.0", manifest.Experts["SecurityArchitect"]);
    }

    [Fact]
    public void ProvidersDefault_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = "sk-test-key"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.NotNull(manifest);
        var profile = manifest.Providers["default"];
        Assert.Equal("openai",      profile.Provider);
        Assert.Equal("gpt-4o-mini", profile.Model);
        Assert.Equal("sk-test-key", profile.ApiKey);
        Assert.Null(profile.Endpoint);
    }

    [Fact]
    public void MultipleProviderProfiles_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = "sk-test"

            [providers.fast]
            provider = "anthropic"
            model    = "claude-haiku-4-5-20251001"
            apiKey   = "sk-ant-test"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Providers.Count);
        Assert.Equal("openai",    manifest.Providers["default"].Provider);
        Assert.Equal("anthropic", manifest.Providers["fast"].Provider);
    }

    [Fact]
    public void EnvCall_NoDefault_ResolvesVariable()
    {
        Environment.SetEnvironmentVariable("FORGE_TEST_KEY", "resolved-from-env");
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = env("FORGE_TEST_KEY")
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("resolved-from-env", manifest!.Providers["default"].ApiKey);
        Environment.SetEnvironmentVariable("FORGE_TEST_KEY", null);
    }

    [Fact]
    public void EnvCall_WithDefault_UsesDefaultWhenUnset()
    {
        Environment.SetEnvironmentVariable("FORGE_MISSING_KEY", null);
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = env("FORGE_MISSING_KEY", "fallback-key")
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("fallback-key", manifest!.Providers["default"].ApiKey);
    }

    [Fact]
    public void ExpertsAndProviders_BothPresent_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [experts]
            Architect = "ghcr.io/katasec/architect@0.1.0"

            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = "sk-test"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.NotNull(manifest);
        Assert.Single(manifest.Experts);
        Assert.Single(manifest.Providers);
    }

    [Fact]
    public void EmptyFile_ReturnsEmptyManifest()
    {
        var path = WriteTempManifest("");

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.NotNull(manifest);
        Assert.Empty(manifest.Experts);
        Assert.Empty(manifest.Providers);
    }

    [Fact]
    public void UnknownProvider_ThrowsForgeTomlException()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider = "groq"
            model    = "llama3"
            apiKey   = "key"
            """);

        Assert.Throws<ForgeTomlException>(() => ForgeTomlReader.TryRead(path));
    }

    [Fact]
    public void UnknownField_ThrowsForgeTomlException()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider    = "openai"
            model       = "gpt-4o-mini"
            apiKey      = "key"
            temperature = "0.7"
            """);

        Assert.Throws<ForgeTomlException>(() => ForgeTomlReader.TryRead(path));
    }

    [Fact]
    public void MissingProviderField_ThrowsForgeTomlException()
    {
        var path = WriteTempManifest("""
            [providers.default]
            model  = "gpt-4o-mini"
            apiKey = "key"
            """);

        Assert.Throws<ForgeTomlException>(() => ForgeTomlReader.TryRead(path));
    }

    [Fact]
    public void WithEndpoint_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [providers.azure]
            provider = "azure"
            model    = "gpt-4o"
            apiKey   = "az-key"
            endpoint = "https://my-org.openai.azure.com/"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("https://my-org.openai.azure.com/", manifest!.Providers["azure"].Endpoint);
    }

    [Fact]
    public void InlineComment_IsIgnored()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"   # use OpenAI
            model    = "gpt-4o-mini"
            apiKey   = "sk-test"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("openai", manifest!.Providers["default"].Provider);
    }

    [Fact]
    public void ExecutionSection_DefaultsApplied_WhenAbsent()
    {
        var path = WriteTempManifest("""
            [providers.default]
            provider = "openai"
            model    = "gpt-4o-mini"
            apiKey   = "sk-test"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("process", manifest!.Execution.Backend);
        Assert.Equal("30s",     manifest!.Execution.DefaultTimeout);
    }

    [Fact]
    public void ExecutionSection_BackendAndTimeout_ParsedCorrectly()
    {
        var path = WriteTempManifest("""
            [execution]
            backend        = "process"
            defaultTimeout = "60s"
            """);

        var manifest = ForgeTomlReader.TryRead(path);

        Assert.Equal("process", manifest!.Execution.Backend);
        Assert.Equal("60s",     manifest!.Execution.DefaultTimeout);
    }

    [Fact]
    public void ExecutionSection_UnknownBackend_Throws()
    {
        var path = WriteTempManifest("""
            [execution]
            backend = "wasm"
            """);

        Assert.Throws<ForgeTomlException>(() => ForgeTomlReader.TryRead(path));
    }

    [Fact]
    public void ExecutionSection_UnknownField_Throws()
    {
        var path = WriteTempManifest("""
            [execution]
            backend    = "process"
            sandboxing = "true"
            """);

        Assert.Throws<ForgeTomlException>(() => ForgeTomlReader.TryRead(path));
    }
}
