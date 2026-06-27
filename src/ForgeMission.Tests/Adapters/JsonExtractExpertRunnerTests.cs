using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;

namespace ForgeMission.Tests.Adapters;

public class JsonExtractExpertRunnerTests
{
    private static ExpertDefinition JsonExtractExpert() =>
        new("ExtractFeatures", "JSON object", "Context bag entries", "", Kind: "json_extract");

    [Fact]
    public async Task RunAsync_NumericFields_StoredAsDouble()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """{"word_count": 245, "avg_sentence_length": 18.3}"""
        };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass", result.Status);
        Assert.IsType<double>(context["word_count"]);
        Assert.Equal(245.0,  (double)context["word_count"]);
        Assert.IsType<double>(context["avg_sentence_length"]);
        Assert.Equal(18.3,   (double)context["avg_sentence_length"], precision: 5);
    }

    [Fact]
    public async Task RunAsync_StringFields_StoredAsString()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """{"label": "high_quality", "summary": "short and clear"}"""
        };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass",          result.Status);
        Assert.Equal("high_quality",  context["label"]);
        Assert.Equal("short and clear", context["summary"]);
    }

    [Fact]
    public async Task RunAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object> { ["output"] = "not valid json" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(expert, context));
        Assert.Contains("json_extract", ex.Message);
        Assert.Contains("neither valid JSON", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Mixed prose + JSON (fenced block)

    [Fact]
    public async Task RunAsync_MixedOutput_ExtractsJsonBlock()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """
                The code has several readability concerns, particularly around nesting.

                ```json
                {"severity": "medium", "refactor_priority": 8}
                ```
                """
        };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass",     result.Status);
        Assert.Equal("medium",   context["severity"]);
        Assert.Equal(8.0,        (double)context["refactor_priority"]);
    }

    [Fact]
    public async Task RunAsync_MixedOutput_ProsePreservedInOutputKey()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """
                The nested loop structure should be extracted into a helper function.

                ```json
                {"severity": "medium"}
                ```
                """
        };

        await runner.RunAsync(expert, context);

        Assert.Contains("nested loop", context["output"].ToString());
    }

    [Fact]
    public async Task RunAsync_MixedOutput_ProseAndJsonBothSides()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """
                Opening analysis here.

                ```json
                {"score": 7}
                ```

                Closing recommendation here.
                """
        };

        await runner.RunAsync(expert, context);

        // Both prose sections should be preserved, JSON keys extracted
        var prose = context["output"].ToString()!;
        Assert.Contains("Opening analysis", prose);
        Assert.Contains("Closing recommendation", prose);
        Assert.Equal(7.0, (double)context["score"]);
    }

    [Fact]
    public async Task RunAsync_PureJson_BackwardsCompatible()
    {
        // No fence — existing pure JSON path unchanged
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """{"label": "pass", "confidence": 0.91}"""
        };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass",  result.Status);
        Assert.Equal("pass",  context["label"]);
        Assert.Equal(0.91,    (double)context["confidence"], precision: 5);
    }

    [Fact]
    public async Task RunAsync_PassThroughOutput_TextUnchanged()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var json    = """{"score": 0.92}""";
        var context = new Dictionary<string, object> { ["output"] = json };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal(json, result.Text);
    }

    [Fact]
    public async Task RunAsync_InjectedKeys_ReadableByDownstreamStep()
    {
        // Simulate: JsonExtractExpertRunner injects keys, then a downstream step reads them.
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = """{"word_count": 100, "vocabulary_richness": 0.75}"""
        };

        await runner.RunAsync(expert, context);

        // Downstream ONNX step would read these as float features.
        Assert.True(context.ContainsKey("word_count"));
        Assert.True(context.ContainsKey("vocabulary_richness"));
        Assert.Equal(100f,   Convert.ToSingle(context["word_count"]));
        Assert.Equal(0.75f,  Convert.ToSingle(context["vocabulary_richness"]));
    }

    [Fact]
    public async Task RunAsync_MissingOutput_ParsesEmptyObjectGracefully()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object>
        {
            ["output"] = "{}"
        };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass", result.Status);
        Assert.Equal("{}", result.Text);
    }
}
