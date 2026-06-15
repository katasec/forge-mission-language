# Phase 18 — Drop MAF, Direct IChatClient Runner

## Goal

Replace `MafExpertRunner` (backed by `Microsoft.Agents.AI`) with a direct `IChatClient`
implementation, removing the last AOT blocker we control and eliminating a DI-heavy
framework dependency that adds no value at our call size.

---

## Context

`MafExpertRunner` wraps every expert call in a `ChatClientAgent` + `AgentThread`. MAF was
introduced in Phase 5 as the path of least resistance for an LLM-backed runner. We now
know it carries a significant cost:

- `Microsoft.Agents.AI` is not AOT-ready (DI reflection, runtime codegen).
- It adds ~two abstraction layers over `IChatClient` that do nothing at our scale.
- Each expert call already creates a **fresh** `AgentThread` — there is no shared session
  state. MAF is buying us nothing that `IChatClient.CompleteAsync()` doesn't already do.

`IChatClient` (from `Microsoft.Extensions.AI`) is AOT-aware and is already in the
dependency graph as the thing we hand to MAF. The replacement is mechanical.

---

## What we keep

| Concern | Current owner | After Phase 18 |
|---------|--------------|----------------|
| Provider selection (openai / azure / anthropic) | `Program.cs` `TryBuildRunner` | unchanged |
| `IChatClient` construction | `Program.cs` `TryBuildRunner` | unchanged |
| Multi-expert orchestration | `PipelineRunner` | unchanged |
| System prompt interpolation | `ContextInterpolator` | unchanged |
| `IExpertRunner` interface | `Core/Runtime/IExpertRunner.cs` | unchanged — still the boundary |
| Streaming to stderr | `PipelineRunner` + `StreamAsync` | unchanged |

Everything above the adapter layer is untouched. `IExpertRunner` remains the seam.

---

## What we lose

Nothing functionally. MAF's `ChatClientAgent` is a thin wrapper that:

1. Builds a `[System, User]` message list.
2. Calls `IChatClient.CompleteAsync()` or `CompleteStreamingAsync()`.
3. Returns the result.

We replicate all three steps in ~30 lines. The `AgentThread` concept (conversation memory)
is never exercised — a fresh thread is created and discarded for every expert call.

---

## Design — `DirectExpertRunner`

Replace `MafExpertRunner` with `DirectExpertRunner` in
`src/ForgeMission.Core/Adapters/DirectExpertRunner.cs`.

### Non-streaming path

```csharp
public async Task<StepEnvelope> RunAsync(
    ExpertDefinition expert,
    Dictionary<string, object> context,
    CancellationToken ct = default)
{
    var (userMessage, systemPrompt) = BuildMessages(expert, context);
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, systemPrompt),
        new(ChatRole.User, userMessage),
    };
    // Instruct the model to return StepEnvelope JSON.
    messages[0] = new(ChatRole.System, systemPrompt + JsonInstruction);

    var response = await _chatClient.CompleteAsync(messages, cancellationToken: ct);
    var json     = response.Message.Text ?? string.Empty;
    return JsonSerializer.Deserialize<StepEnvelope>(json, _jsonOptions)
        ?? throw new InvalidOperationException("Expert returned malformed envelope.");
}
```

### Streaming path

```csharp
public async IAsyncEnumerable<string> StreamAsync(
    ExpertDefinition expert,
    Dictionary<string, object> context,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var (userMessage, systemPrompt) = BuildMessages(expert, context);
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, systemPrompt + JsonInstruction),
        new(ChatRole.User, userMessage),
    };

    await foreach (var update in _chatClient.CompleteStreamingAsync(messages, cancellationToken: ct))
    {
        if (!string.IsNullOrEmpty(update.Text))
            yield return update.Text;
    }
}
```

`BuildMessages` and `JsonInstruction` are identical to what `MafExpertRunner` already has —
this is a mechanical lift, not a redesign.

---

## Package changes

### `ForgeMission.Core.csproj`

Remove:
```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0" />
```

Keep:
```xml
<PackageReference Include="Microsoft.Extensions.AI" Version="..." />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="..." />
```

### `ForgeMission.Cli.csproj`

No changes — CLI already references `OpenAI` and `Azure.AI.OpenAI` directly for client
construction, which is unchanged.

---

## Remaining AOT work (out of scope for Phase 18)

After this phase, the remaining AOT issues are:

| Issue | Fix | Complexity |
|-------|-----|-----------|
| `YamlDotNet` reflection serialization | Switch to `StaticSerializerBuilder` / `StaticDeserializerBuilder` | Low |
| `System.Text.Json` reflection in `PipelineRunner.ParseStreamedEnvelope` | Add `[JsonSerializable(typeof(StepEnvelope))]` source-gen context | Low |
| `Azure.AI.OpenAI` AOT warnings | Outside our control — live with warnings on the Azure provider path | N/A |

These are tracked as follow-up items, not blockers for Phase 18.

---

## Tasks

- [x] Create `DirectExpertRunner.cs` in `src/ForgeMission.Core/Adapters/`
- [x] Update `Program.cs`: replace `new MafExpertRunner(chatClient)` → `new DirectExpertRunner(chatClient)`
- [x] Remove `Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` from `ForgeMission.Core.csproj`
- [x] Delete `MafExpertRunner.cs`
- [x] Update `docs/design/architecture.md`: rename "MAF Adapter" → "Direct IChatClient Adapter", remove MAF from dependency diagram
- [x] `dotnet build` — confirm no compile errors
- [x] `dotnet test` — all existing tests pass (runner is stubbed in tests, so no test changes expected)
- [x] Manual smoke test: `forge run` on `missions/build-operator`

## Completion condition

`Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` do not appear in any `.csproj` file.
`forge run` on the build-operator mission produces output. All tests pass.

## Notes

- `Microsoft.Extensions.AI` v10.7.0 renamed `CompleteAsync`/`CompleteStreamingAsync` to
  `GetResponseAsync`/`GetStreamingResponseAsync`. The structured-output overload is
  `GetResponseAsync<T>` from `ChatClientStructuredOutputExtensions` in the full
  `Microsoft.Extensions.AI` package (not just Abstractions).
- `AsIChatClient()` is in `Microsoft.Extensions.AI.OpenAI` — added explicitly to both
  `ForgeMission.Cli` and `ForgeMission.Tests` after MAF transitively provided it before.
- Integration test file renamed from `MafExpertRunnerIntegrationTests.cs` →
  `DirectExpertRunnerIntegrationTests.cs`; class and runner references updated.
