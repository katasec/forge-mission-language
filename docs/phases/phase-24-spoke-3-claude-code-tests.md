# Phase 24 — Spoke 3: Claude Code Integration Tests

**Status:** Done — test passing  
**Hub:** [phase-24-copilot-sdk-integration-tests.md](phase-24-copilot-sdk-integration-tests.md)  
**Depends on:** Spoke 1 (test harness)  
**Parallel with:** Spoke 2  
**Gates on:** Spoke 2 passing — implement this only after the Copilot SDK pattern is proven

## Goal

Drive `OaiServer` (wrapping an MCL mission) via the `claude` CLI process, proving
that Claude Code users get the same forge-in-the-middle experience. There is no
C# SDK for Claude Code; the CLI is driven as a subprocess via its
`--output-format json` non-interactive mode with `ANTHROPIC_BASE_URL` overridden
to point at the test server.

---

## Prerequisites

| Var / binary | Purpose |
|---|---|
| `claude` on `PATH` | Claude Code CLI installed (`npm i -g @anthropic-ai/claude-code`) |
| `MCL_API_KEY` | Anthropic or OpenAI key for the MCL pipeline |
| `MCL_MODEL` | Model for the pipeline (default: `claude-3-5-haiku-20241022`) |
| `ANTHROPIC_API_KEY` | Key Claude Code itself uses to call the LLM *after* forge |

All tests in this class are `[Trait("Category", "Integration")]` and skipped if
`claude` is not on `PATH` or the required keys are absent.

---

## How `claude` non-interactive mode works

```bash
claude -p "your prompt" --output-format json
```

Exits after one turn. Stdout is a JSON object:

```json
{
  "type": "result",
  "result": "The assistant's reply text",
  "session_id": "...",
  "cost_usd": 0.001
}
```

`ANTHROPIC_BASE_URL=http://localhost:{port}/v1` redirects Claude Code's API calls
to `OaiServer`. The `ANTHROPIC_API_KEY` must still be set (Claude Code validates
it exists) but the value can be `"forge"` since the OAI server ignores it —
authentication is handled by the MCL pipeline's own `MCL_API_KEY`.

---

## Tasks

### 1. Create `ClaudeCodeTests.cs`

File: `src/ForgeMission.Tests/Integration/ClaudeCodeTests.cs`

```csharp
using System.Diagnostics;
using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Runtime;
using ForgeMission.Tests.Runtime;

namespace ForgeMission.Tests.Integration;

public sealed class ClaudeCodeTests
{
    // ------------------------------------------------------------------
    // Live: Claude Code CLI → forge OaiServer → real LLM pipeline
    // ------------------------------------------------------------------
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaudeCode_LiveRoundTrip_ThroughNoopMission()
    {
        SkipIfMissing("MCL_API_KEY");
        SkipIfMissing("ANTHROPIC_API_KEY");
        SkipIfNotOnPath("claude");

        var apiKey     = Environment.GetEnvironmentVariable("MCL_API_KEY")!;
        var model      = Environment.GetEnvironmentVariable("MCL_MODEL")
                             ?? "claude-3-5-haiku-20241022";
        var missionDir = Path.Combine(AppContext.BaseDirectory, "Missions", "noop");

        var source   = await File.ReadAllTextAsync(Path.Combine(missionDir, "mission.mcl"));
        var ast      = ForgeMission.Parser.MclParser.Parse(source);
        var lockFile = ForgeMission.Core.Resolution.LockFileIO.Read(
                           Path.Combine(missionDir, "mcl.lock"));
        var experts  = ForgeMission.Core.Experts.ExpertLoader
                           .LoadFromLockFile(lockFile, missionDir);

        var runner   = BuildLiveRunner(apiKey, model);
        var mcClient = new MissionChatClient(ast, experts, runner);

        await using var fixture = await OaiServerFixture.StartAsync(mcClient);

        // Act — run `claude` with ANTHROPIC_BASE_URL pointing at forge
        var (exitCode, stdout, stderr) = await RunClaudeAsync(
            prompt:      "Say exactly: forge works",
            baseUrl:     $"{fixture.BaseUrl}/v1",
            timeoutMs:   60_000);

        // Assert
        Assert.Equal(0, exitCode);
        var json  = JsonDocument.Parse(stdout).RootElement;
        var reply = json.GetProperty("result").GetString() ?? string.Empty;
        Assert.Contains("forge works", reply, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunClaudeAsync(
        string prompt,
        string baseUrl,
        int timeoutMs)
    {
        var psi = new ProcessStartInfo("claude", $"-p \"{prompt}\" --output-format json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        // Override API endpoint so Claude Code hits OaiServer instead of Anthropic
        psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl;
        // ANTHROPIC_API_KEY must be set; forge ignores its value but claude validates presence
        psi.Environment["ANTHROPIC_API_KEY"]  =
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "forge";

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();

        var finished = await Task.Run(() => proc.WaitForExit(timeoutMs));
        if (!finished) { proc.Kill(); throw new TimeoutException("claude timed out"); }

        return (proc.ExitCode, stdout, stderr);
    }

    private static IExpertRunner BuildLiveRunner(string apiKey, string model)
    {
        var chatClient = new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential(apiKey))
            .GetChatClient(model)
            .AsIChatClient();
        return new DirectExpertRunner(chatClient);
    }

    private static void SkipIfMissing(string envVar)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
            throw new SkipException($"Skipped: {envVar} not set");
    }

    private static void SkipIfNotOnPath(string binary)
    {
        var found = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, binary))
                     || File.Exists(Path.Combine(dir, binary + ".exe")));
        if (!found)
            throw new SkipException($"Skipped: '{binary}' not found on PATH");
    }
}
```

> **Note:** `SkipException` is xUnit v3's skip mechanism. For xUnit v2 use
> `Skip.If(condition, reason)` or a custom `SkippableFactAttribute` from the
> `Xunit.SkippableFact` NuGet package.

---

## Differences from Copilot SDK approach (Spoke 2)

| | Copilot SDK (Spoke 2) | Claude Code (Spoke 3) |
|---|---|---|
| Transport | JSON-RPC / stdio to CLI subprocess | HTTP from CLI subprocess |
| API override | `ProviderConfig.BaseUrl` | `ANTHROPIC_BASE_URL` env var |
| Response parsing | Typed SDK events | JSON stdout parse |
| Auth | `COPILOT_GITHUB_TOKEN` | `ANTHROPIC_API_KEY` (value ignored by forge) |
| SDK available? | Yes — `GitHub.Copilot.SDK` NuGet | No — subprocess only |

---

## Acceptance criteria

- Test is **skipped** when `claude` is not on `PATH` or keys are absent
- Test passes when all prerequisites are met and the reply contains the expected phrase
- `claude` process exits with code 0
- Stdout is valid JSON with a `result` field
