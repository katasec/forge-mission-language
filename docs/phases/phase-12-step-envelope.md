# Phase 12 — StepEnvelope

**Status:** Pending

## Goal

Replace the raw `string` flowing between experts with a structured `StepEnvelope`. Every expert
produces a typed, JSON-shaped result. The runtime reads `status` to enforce fail-fast behaviour.
Any step returning `fail` immediately stops the pipeline and fails the mission.

## Motivation

- A plain string gives the runtime no signal about whether a step succeeded or not
- Pass/fail needs to be a first-class concern at every step, not inferred from text patterns
- The envelope is the FML equivalent of ASP.NET Core's `HttpContext` — a generic, open-ended
  container that flows through the pipeline and can be enriched by any step

## StepEnvelope Shape

```csharp
public record StepEnvelope(
    string Text,                                        // content forwarded to next expert
    string Status = "pass",                             // "pass" | "fail"
    string? Reason = null,                              // why — set on fail, optional on pass
    IReadOnlyDictionary<string, JsonElement>? Meta = null  // open-ended expert-specific fields
);
```

`Meta` is the open-ended extension point. Examples: `confidence`, `issues_found`, `word_count`.
Downstream experts can read from it; the runtime forwards it untouched.

## JSON Output Contract

The runtime injects a JSON schema instruction into every expert's system prompt. Expert authors
do not write this — it is invisible to them. The LLM must respond with:

```json
{
  "text": "...",
  "status": "pass",
  "reason": null,
  "meta": {}
}
```

OpenAI JSON mode (`response_format: json_object`) is used to guarantee valid JSON.

## Expert Authoring Convention

- **Analytical steps** (critics, checkers): always return `"status": "pass"`. Put findings in `meta`.
  Returning `fail` would short-circuit the pipeline — only do it for genuine blockers.
- **Verdict steps** (judges, auditors): the only steps that should return `"status": "fail"`.
  These are the declared quality gates.

## Fail-Fast Rule

Strict mode by default — like `"use strict"` in JavaScript. Any step returning `"status": "fail"`
stops the pipeline immediately. The mission fails with that step's `reason`.

## Changes Required

| File | Change |
|------|--------|
| `IExpertRunner.cs` | Return `Task<StepEnvelope>` instead of `Task<string>` |
| `MafExpertRunner.cs` | Enable JSON mode; inject schema instruction; parse response into `StepEnvelope` |
| `PipelineRunner.cs` | Check `envelope.Status` after each step; fail-fast; extract `Text` for context bag |
| `MissionResult.cs` | Add `Status` (pass/fail) and `FailReason` (which step failed and why) |
| `StubExpertRunner.cs` | Return `StepEnvelope` from stub |
| Tests | Update all runner tests; add fail-fast test |

## Tests to Add

- `StepFail_StopsImmediately` — second step never called when first returns fail
- `FailReason_PropagatedToMissionResult` — `FailReason` carries step name + reason
- `MetaFields_ForwardedToNextStep` — meta from step N is accessible in context for step N+1
- `JsonMode_ParsesCleanly` — integration test: MAF returns valid envelope
