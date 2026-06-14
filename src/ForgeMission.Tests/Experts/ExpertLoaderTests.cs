using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;

namespace ForgeMission.Tests.Experts;

public class ExpertLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ExpertLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteExpert(string filename, string content)
    {
        var path = Path.Combine(_dir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private static string ValidExpertMarkdown(
        string name   = "KubernetesArchitect",
        string input  = "MissionBrief",
        string output = "ArchitectureProposal",
        string body   = "You are a Kubernetes platform architect.") => $"""
        ---
        name: {name}
        input: {input}
        output: {output}
        ---

        {body}
        """;

    [Fact]
    public void LoadAll_ValidExpertFile_LoadsCorrectly()
    {
        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown());

        var experts = new ExpertLoader(_dir).LoadAll();

        Assert.Single(experts);
        Assert.True(experts.ContainsKey("KubernetesArchitect"));
    }

    [Fact]
    public void LoadAll_ParsesFrontmatterFields()
    {
        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown());

        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];

        Assert.Equal("KubernetesArchitect", expert.Name);
        Assert.Equal("MissionBrief", expert.Input);
        Assert.Equal("ArchitectureProposal", expert.Output);
    }

    [Fact]
    public void LoadAll_BodyBelowFrontmatter_BecomesSystemPrompt()
    {
        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown(body: "You are a Kubernetes platform architect."));

        var expert = new ExpertLoader(_dir).LoadAll()["KubernetesArchitect"];

        Assert.Contains("You are a Kubernetes platform architect.", expert.SystemPrompt);
    }

    [Fact]
    public void LoadAll_MultipleExperts_LoadsAll()
    {
        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        WriteExpert("SecurityArchitect.md",   ValidExpertMarkdown("SecurityArchitect",   "ArchitectureProposal", "SecurityReview"));
        WriteExpert("PrincipalReviewer.md",   ValidExpertMarkdown("PrincipalReviewer",   "SecurityReview", "FinalReport"));

        var experts = new ExpertLoader(_dir).LoadAll();

        Assert.Equal(3, experts.Count);
        Assert.True(experts.ContainsKey("KubernetesArchitect"));
        Assert.True(experts.ContainsKey("SecurityArchitect"));
        Assert.True(experts.ContainsKey("PrincipalReviewer"));
    }

    [Fact]
    public void LoadAll_MissingFrontmatterField_ThrowsExpertLoadException()
    {
        WriteExpert("Bad.md", "---\nname: Bad\n---\nBody");

        var ex = Assert.Throws<ExpertLoadException>(() => new ExpertLoader(_dir).LoadAll());
        Assert.Contains("input", ex.Message);
    }

    [Fact]
    public void Validate_MissingExpert_ThrowsExpertLoadException()
    {
        var ast = FmlParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Assert.Throws<ExpertLoadException>(() => ExpertLoader.Validate(ast, experts));
        Assert.Contains("SecurityArchitect", ex.Message);
    }

    [Fact]
    public void Validate_AllExpertsPresent_DoesNotThrow()
    {
        var ast = FmlParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        WriteExpert("KubernetesArchitect.md", ValidExpertMarkdown("KubernetesArchitect", "MissionBrief", "ArchitectureProposal"));
        WriteExpert("SecurityArchitect.md",   ValidExpertMarkdown("SecurityArchitect", "ArchitectureProposal", "SecurityReview"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExpertDeclaredInAst_DoesNotRequireMarkdownFile()
    {
        var ast = FmlParser.Parse("""
            expert KubernetesArchitect =
                RequirementsAnalyst
                |> PlatformArchitect

            mission BuildOperator =
                KubernetesArchitect
            """);

        WriteExpert("RequirementsAnalyst.md", ValidExpertMarkdown("RequirementsAnalyst", "Brief", "Analysis"));
        WriteExpert("PlatformArchitect.md",   ValidExpertMarkdown("PlatformArchitect", "Analysis", "Design"));
        var experts = new ExpertLoader(_dir).LoadAll();

        var ex = Record.Exception(() => ExpertLoader.Validate(ast, experts));
        Assert.Null(ex);
    }
}
