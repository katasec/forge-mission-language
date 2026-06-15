# Phase 14 — output() Retry

**Status:** Pending — depends on Phase 12 (StepEnvelope) and Phase 13 (passes when)

## Goal

Allow a mission to re-run automatically when it fails, up to a declared attempt cap.
Declared on the `output()` statement — the place that already controls routing.

## Motivation

LLM inference is non-deterministic. A judged mission may fail on one run and pass on the next
(observed in the elevator-pitch demo: Fail on run 1, Pass on run 2). Without retry, the user
must re-run manually. With retry, the mission loops until it passes or exhausts its attempts.

## Syntax

```
output(RefinedPitch) retry 3
output(RefinedPitch, "./pitch.md") retry 5
```

Without `retry`: run once, surface result regardless of status.
With `retry N`: run up to N times, stop early on first pass.

## Runtime Behaviour

```
attempt 1 → fail → attempt 2 → fail → attempt 3 → pass → output result
attempt 1 → fail → attempt 2 → fail → attempt 3 → fail → mission fails, surface last result
```

Each retry is a full fresh run of the mission pipeline — context is re-seeded from scratch.
The attempt number is available in context as `{{attempt}}` so experts can adjust behaviour
(e.g. "this is attempt 2 — be stricter").

## Grammar Changes

```antlr
outputDecl
    : OUTPUT LPAREN UPPER_ID (COMMA STRING)? RPAREN retryClause?
    ;

retryClause
    : RETRY INT
    ;
```

New lexer tokens: `RETRY`, `INT`.

## AST Changes

```csharp
public record OutputDeclaration(
    string MissionName,
    string? FilePath,
    int MaxAttempts = 1);       // 1 = no retry (default)
```

## MissionResult Changes

```csharp
public record MissionResult(
    string MissionName,
    string Text,
    MissionStatus Status,
    string? FailReason = null,
    int Attempts = 1);          // how many runs it took
```

## CLI Output

```
Running mission 'RefinedPitch'... (attempt 1/3)
Running mission 'RefinedPitch'... (attempt 2/3)
```

Status lines to stderr. Final result to stdout as normal.

## Changes Required

| File | Change |
|------|--------|
| `FmlGrammar.g4` | Add `retryClause`, `RETRY`, `INT` tokens |
| `Ast.cs` | Add `MaxAttempts` to `OutputDeclaration` |
| `FmlAstBuilder.cs` | Visit `retryClause` |
| `Program.cs` | Retry loop in `run` command; pass attempt number in vars |
| `MissionResult.cs` | Add `Attempts` field |
| Tests | Retry stops on first pass; exhausted retries surfaces last result |
