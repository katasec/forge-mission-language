using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;

namespace ForgeMission.Tests.Integration;

public sealed class ClaudeCodeTests
{
    // ------------------------------------------------------------------
    // Live: Claude Code CLI → forge OaiServer → real LLM pipeline
    // ------------------------------------------------------------------
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ClaudeCode_LiveRoundTrip_ThroughNoopMission()
    {
        var apiKey       = Environment.GetEnvironmentVariable("MCL_API_KEY");
        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey),       "MCL_API_KEY not set");
        Skip.If(string.IsNullOrWhiteSpace(anthropicKey), "ANTHROPIC_API_KEY not set");
        Skip.If(!IsOnPath("claude"),                     "'claude' not found on PATH");

        var model    = Environment.GetEnvironmentVariable("MCL_MODEL") ?? "claude-3-5-haiku-20241022";
        // Use a direct IChatClient so AnthropicServer forwards the full conversation
        // history to the LLM. MissionChatClient only passes the last user message as
        // the mission goal, which breaks claude CLI's multi-turn internal reasoning.
        var chatClient = BuildDirectChatClient(apiKey!, model);

        await using var fixture = await AnthropicServerFixture.StartAsync(chatClient);

        var (exitCode, stdout, stderr) = await RunClaudeAsync(
            prompt:    "Say exactly: forge works",
            baseUrl:   fixture.BaseUrl,
            apiKey:    anthropicKey!,
            timeoutMs: 60_000);

        Assert.True(exitCode == 0, $"claude exited {exitCode}.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
        var json  = JsonDocument.Parse(stdout).RootElement;
        var reply = json.GetProperty("result").GetString() ?? string.Empty;
        Assert.Contains("forge works", reply, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    // Direct client preserves the full conversation history so claude CLI's
    // multi-turn internal reasoning (title generation, state tracking, etc.) works.
    private static IChatClient BuildDirectChatClient(string apiKey, string model) =>
        new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetChatClient(model)
            .AsIChatClient();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunClaudeAsync(
        string prompt,
        string baseUrl,
        string apiKey,
        int timeoutMs)
    {
        var psi = new ProcessStartInfo("claude", $"-p \"{prompt}\" --output-format json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        // Redirect Claude Code's API calls to OaiServer instead of Anthropic
        psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl;
        // claude validates that the key is present; forge ignores its value
        psi.Environment["ANTHROPIC_API_KEY"]  = apiKey;

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();

        var finished = await Task.Run(() => proc.WaitForExit(timeoutMs));
        if (!finished) { proc.Kill(); throw new TimeoutException("claude CLI timed out"); }

        return (proc.ExitCode, stdout, stderr);
    }

    private static bool IsOnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, binary))
                     || File.Exists(Path.Combine(dir, binary + ".exe")));
}
