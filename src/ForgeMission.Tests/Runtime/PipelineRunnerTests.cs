using ForgeMission.Core.Experts;
using ForgeMission.Parser;
using ForgeMission.Core.Runtime;
using static ForgeMission.Core.Runtime.MissionStatus;

namespace ForgeMission.Tests.Runtime;

public class PipelineRunnerTests
{
    private static ExpertDefinition Expert(string name) =>
        new(name, "Input", "Output", $"You are {name}.");

    private static Dictionary<string, ExpertDefinition> Experts(params string[] names) =>
        names.ToDictionary(n => n, Expert, StringComparer.Ordinal);

    [Fact]
    public async Task SingleStep_CallsRunnerOnce_ReturnsOutput()
    {
        var ast    = MclParser.Parse("mission BuildOperator = KubernetesArchitect");
        var stub   = new StubExpertRunner();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Single(stub.Calls);
        Assert.Equal("KubernetesArchitect", stub.Calls[0].ExpertName);
        Assert.Equal("BuildOperator", result.MissionName);
    }

    [Fact]
    public async Task MultiStep_PassesOutputForward()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
                |> PrincipalReviewer
            """);

        var stub = new StubExpertRunner((name, ctx) =>
            $"Output from {name} given [{ctx["output"]}]");

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect", "PrincipalReviewer"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal(3, stub.Calls.Count);
        Assert.Equal(string.Empty,
            stub.Calls[0].Context["output"].ToString());
        Assert.Equal("Output from KubernetesArchitect given []",
            stub.Calls[1].Context["output"].ToString());
        Assert.Equal("Output from SecurityArchitect given [Output from KubernetesArchitect given []]",
            stub.Calls[2].Context["output"].ToString());
    }

    [Fact]
    public async Task Result_ContainsLastStepOutput()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> PrincipalReviewer
            """);

        var stub   = new StubExpertRunner((name, _) => $"Output from {name}");
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "PrincipalReviewer"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal("Output from PrincipalReviewer", result.Text);
    }

    [Fact]
    public async Task StepWriter_ReceivesEachStepOutput()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        var sw     = new StringWriter();
        var stub   = new StubExpertRunner((name, _) => $"Output from {name}");
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator", StepWriter: sw));

        var written = sw.ToString();
        Assert.Contains("→ KubernetesArchitect...", written);
        Assert.Contains("→ SecurityArchitect...", written);
        Assert.Contains("Output from KubernetesArchitect", written);
        Assert.Contains("Output from SecurityArchitect", written);
    }

    [Fact]
    public async Task CancellationToken_IsRespected()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                          new PipelineRunOptions("BuildOperator"),
                          cts.Token));
    }

    [Fact]
    public async Task LetBindings_SeedContext()
    {
        var ast = MclParser.Parse("""
            let goal = "Design a K8s operator"
            mission BuildOperator = KubernetesArchitect
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("Design a K8s operator", stub.Calls[0].Context["goal"].ToString());
    }

    [Fact]
    public async Task WithClause_OverridesContextForStep()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect with { style = "terse" }
            """);

        var stub = new StubExpertRunner();
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"), new PipelineRunOptions("BuildOperator"));

        Assert.Equal("terse", stub.Calls[0].Context["style"].ToString());
    }

    [Fact]
    public async Task VarFlag_OverridesLetBinding()
    {
        var ast = MclParser.Parse("""
            let goal = "original"
            mission BuildOperator = KubernetesArchitect
            """);

        var stub = new StubExpertRunner();
        var vars = new Dictionary<string, string> { ["goal"] = "overridden" };
        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect"),
                      new PipelineRunOptions("BuildOperator", vars));

        Assert.Equal("overridden", stub.Calls[0].Context["goal"].ToString());
    }

    [Fact]
    public void MissingEnvVar_ThrowsClearly()
    {
        var ast = MclParser.Parse("""
            let apiKey = env("FMLTEST_MISSING_VAR_XYZ")
            mission BuildOperator = KubernetesArchitect
            """);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("KubernetesArchitect"),
                          new PipelineRunOptions("BuildOperator"))
                .GetAwaiter().GetResult());

        Assert.Contains("FMLTEST_MISSING_VAR_XYZ", ex.Message);
    }

    // Phase 17 — provider configuration bindings

    [Fact]
    public async Task ProviderBindings_AllFourSeededInContext()
    {
        var ast = MclParser.Parse("""
            let provider = env("MCL_PROVIDER", "openai")
            let apiKey   = env("MCL_API_KEY")
            let model    = env("MCL_MODEL", "gpt-4o-mini")
            let endpoint = env("MCL_ENDPOINT", "")
            mission Demo = Worker
            """);

        Environment.SetEnvironmentVariable("MCL_API_KEY", "sk-test");
        try
        {
            var stub = new StubExpertRunner();
            await new PipelineRunner(stub)
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

            var ctx = stub.Calls[0].Context;
            Assert.Equal("openai",      ctx["provider"].ToString());
            Assert.Equal("sk-test",     ctx["apiKey"].ToString());
            Assert.Equal("gpt-4o-mini", ctx["model"].ToString());
            Assert.Equal("",            ctx["endpoint"].ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCL_API_KEY", null);
        }
    }

    [Fact]
    public async Task ProviderBinding_VarFlagOverridesProvider()
    {
        var ast = MclParser.Parse("""
            let provider = env("MCL_PROVIDER", "openai")
            let apiKey   = env("MCL_API_KEY")
            let model    = env("MCL_MODEL", "gpt-4o-mini")
            mission Demo = Worker
            """);

        Environment.SetEnvironmentVariable("MCL_API_KEY", "sk-test");
        try
        {
            var stub = new StubExpertRunner();
            var vars = new Dictionary<string, string> { ["provider"] = "azure" };
            await new PipelineRunner(stub)
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo", vars));

            Assert.Equal("azure", stub.Calls[0].Context["provider"].ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCL_API_KEY", null);
        }
    }

    [Fact]
    public void MissingApiKey_ThrowsClearly()
    {
        var ast = MclParser.Parse("""
            let apiKey = env("MCL_API_KEY")
            mission Demo = Worker
            """);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner(new StubExpertRunner())
                .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"))
                .GetAwaiter().GetResult());

        Assert.Contains("MCL_API_KEY", ex.Message);
    }

    // Phase 12 — StepEnvelope / fail-fast

    [Fact]
    public async Task StepFail_StopsImmediately_SecondStepNeverCalled()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        var stub = new StubExpertRunner((name, _) =>
            name == "KubernetesArchitect"
                ? new StepEnvelope("bad output", "fail", "quality too low")
                : new StepEnvelope($"Output from {name}"));

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Single(stub.Calls);
        Assert.Equal(MissionStatus.Fail, result.Status);
        Assert.Contains("KubernetesArchitect", result.FailReason);
        Assert.Contains("quality too low", result.FailReason);
    }

    [Fact]
    public async Task StepPass_PipelineContinues()
    {
        var ast = MclParser.Parse("""
            mission BuildOperator =
                KubernetesArchitect
                |> SecurityArchitect
            """);

        var stub   = new StubExpertRunner();
        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("KubernetesArchitect", "SecurityArchitect"),
                      new PipelineRunOptions("BuildOperator"));

        Assert.Equal(2, stub.Calls.Count);
        Assert.Equal(MissionStatus.Pass, result.Status);
    }

    // Phase 14 — loop N

    [Fact]
    public async Task Loop_RetriesUntilAllStepsPass()
    {
        var ast = MclParser.Parse("""
            mission RefinedPitch =
                Drafter
                |> Judge
                loop 3
            """);

        var callCount = 0;
        var stub = new StubExpertRunner((name, _) =>
        {
            callCount++;
            if (name == "Judge" && callCount <= 2)
                return new StepEnvelope("fail output", "fail", "not good enough");
            return new StepEnvelope($"Output from {name}");
        });

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Drafter", "Judge"),
                      new PipelineRunOptions("RefinedPitch"));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task Loop_ExhaustedFails_SurfacesLastFailReason()
    {
        var ast = MclParser.Parse("""
            mission RefinedPitch =
                Drafter
                |> Judge
                loop 3
            """);

        var stub = new StubExpertRunner((name, _) =>
            name == "Judge"
                ? new StepEnvelope("bad output", "fail", "never good enough")
                : new StepEnvelope($"Output from {name}"));

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Drafter", "Judge"),
                      new PipelineRunOptions("RefinedPitch"));

        Assert.Equal(MissionStatus.Fail, result.Status);
        Assert.Equal(3, result.Attempts);
        Assert.Contains("never good enough", result.FailReason);
    }

    [Fact]
    public async Task AttemptVariable_InjectedEachAttempt()
    {
        var ast = MclParser.Parse("""
            mission Demo =
                Worker
                loop 3
            """);

        var attempts = new List<string>();
        var stub = new StubExpertRunner((name, ctx) =>
        {
            attempts.Add(ctx["attempt"].ToString()!);
            return new StepEnvelope("done", "fail", "retry");
        });

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.Equal(["1", "2", "3"], attempts);
    }

    [Fact]
    public async Task MaxLoopsVariable_InjectedEachAttempt()
    {
        var ast = MclParser.Parse("""
            mission Demo =
                Worker
                loop 5
            """);

        var maxLoopsValues = new List<string>();
        var stub = new StubExpertRunner((name, ctx) =>
        {
            maxLoopsValues.Add(ctx["max_loops"].ToString()!);
            return new StepEnvelope("done");
        });

        await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.All(maxLoopsValues, v => Assert.Equal("5", v));
    }

    [Fact]
    public async Task NoLoop_AttemptAndMaxLoopsDefaultToOne()
    {
        var ast  = MclParser.Parse("mission Demo = Worker");
        var stub = new StubExpertRunner();

        var result = await new PipelineRunner(stub)
            .RunAsync(ast, Experts("Worker"), new PipelineRunOptions("Demo"));

        Assert.Equal("1", stub.Calls[0].Context["attempt"].ToString());
        Assert.Equal("1", stub.Calls[0].Context["max_loops"].ToString());
        Assert.Equal(1, result.Attempts);
    }

    [Fact]
    public void LoopN_ParsedIntoMissionDeclaration()
    {
        var ast     = MclParser.Parse("mission Demo = Worker loop 4");
        var mission = ast.Declarations.OfType<MissionDeclaration>().Single();
        Assert.Equal(4, mission.MaxLoops);
    }

    [Fact]
    public void NoLoop_MaxLoopsDefaultsToOne()
    {
        var ast     = MclParser.Parse("mission Demo = Worker");
        var mission = ast.Declarations.OfType<MissionDeclaration>().Single();
        Assert.Equal(1, mission.MaxLoops);
    }
}
