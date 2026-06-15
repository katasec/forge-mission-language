# Phase 15 — Token Streaming

**Status:** Pending — depends on Phase 12 (StepEnvelope)

## Goal

Stream LLM tokens to the terminal as they are generated, eliminating the silent wait between
`→ ExpertName...` appearing and the output landing. Each expert's response appears character
by character in real time.

## Motivation

With `--steps`, users see `→ ExpertName...` but then wait silently for the full response.
On a 3-expert pipeline at ~5s per expert, that's 3 silent gaps of up to 15s total. Streaming
closes the gap — the user sees tokens arriving immediately and knows the model is working.

## Interface Change

```csharp
// Before (Phase 12)
Task<StepEnvelope> RunAsync(ExpertDefinition, Dictionary<string, object>, CancellationToken);

// After (Phase 15)
IAsyncEnumerable<string> StreamAsync(ExpertDefinition, Dictionary<string, object>, CancellationToken);
```

`PipelineRunner` collects chunks into a `StringBuilder` to assemble the full text for context
forwarding and envelope parsing. Chunks are forwarded to `StepWriter` as they arrive.

## StepEnvelope and Streaming

The structured JSON envelope (Phase 12) must still be parsed from the complete assembled text.
Two options:

1. **Streaming text, JSON at end** — stream the `text` field token by token; the JSON wrapper
   is parsed only after the stream completes. Requires the LLM to stream the full JSON and the
   runtime to extract `text` from the assembled output for live display.

2. **Separate calls** — stream the prose response for display; make a second lightweight
   structured call to extract `status`/`reason`/`meta`. Doubles LLM calls but keeps
   the streaming UX completely clean.

Option 1 is preferred: one call, stream the JSON, display only the `text` portion live by
buffering until the `"text":` key starts and stopping at the closing quote.

## CLI Behaviour

```
→ KubernetesArchitect...
[tokens stream here in real time to stderr]

→ SecurityArchitect...
[tokens stream here in real time to stderr]
```

Without `--steps`: silent as today. Streaming only activates when `StepWriter` is set.

## Changes Required

| File | Change |
|------|--------|
| `IExpertRunner.cs` | Add `StreamAsync` returning `IAsyncEnumerable<string>` |
| `MafExpertRunner.cs` | Implement streaming via MAF/OpenAI streaming API |
| `PipelineRunner.cs` | Use `StreamAsync` when `StepWriter` is set; buffer to assemble full text |
| `StubExpertRunner.cs` | Implement `StreamAsync` returning single-chunk stream |
| Tests | Streaming test: chunks arrive before method returns; assembled text matches full output |
