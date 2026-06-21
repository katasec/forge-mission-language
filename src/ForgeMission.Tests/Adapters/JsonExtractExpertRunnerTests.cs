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
    public async Task RunAsync_InvalidJson_ThrowsJsonException()
    {
        var runner  = new JsonExtractExpertRunner();
        var expert  = JsonExtractExpert();
        var context = new Dictionary<string, object> { ["output"] = "not valid json" };

        await Assert.ThrowsAnyAsync<JsonException>(() => runner.RunAsync(expert, context));
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
