# Phase 13 — passes when

**Status:** Pending — depends on Phase 12 (StepEnvelope)

## Goal

Allow a mission to declare its own success condition. `passes when <StepName>` names the step
whose envelope status is the designated quality gate for the mission. Serves as both runtime
behaviour and human-readable documentation of intent.

## Motivation

Different missions have different definitions of "done":

- `BuildOperatorDesign` — done when output is generated. Default completion = pass.
- `RefinedPitch` — done when the judge approves. Explicit quality gate required.
- Future `SecurityAudit` — done when zero critical findings.

Without `passes when`, a reader must infer the intent from the pipeline shape. With it, the
mission declares its contract explicitly — like a function signature.

## Syntax

```
mission RefinedPitch(product) =
    PitchDrafter
    |> PitchCritic
    |> PitchReviser
    |> PitchJudge
    passes when PitchJudge

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
    // no passes when — completion = pass (default)
```

## Runtime Behaviour

Fail-fast (Phase 12) still applies universally — any step returning `fail` stops the pipeline.
`passes when` does not relax this. Its role is to declare which step is the *intended* source
of pass/fail, so tooling, logs, and humans know where the bar is.

## Grammar Changes

```antlr
mission
    : MISSION UPPER_ID params? EQUALS pipeline passesWhen?
    ;

passesWhen
    : PASSES WHEN UPPER_ID
    ;
```

New lexer tokens: `PASSES`, `WHEN`.

## AST Changes

```csharp
public record MissionDeclaration(
    string Name,
    IReadOnlyList<string> Params,
    Pipeline Pipeline,
    string? PassesWhen)          // null = completion-based (default)
    : Declaration(Name);
```

## Future Extension

```
passes when PitchJudge and SecurityAudit   // all named steps must pass
passes when any(PitchJudge, BackupJudge)   // lenient — either passes
```

Not in scope for this phase. The grammar should be designed to accommodate these without
breaking changes to the single-step form.

## Changes Required

| File | Change |
|------|--------|
| `FmlGrammar.g4` | Add `passesWhen` rule, `PASSES` and `WHEN` tokens |
| `Ast.cs` | Add `PassesWhen` to `MissionDeclaration` |
| `FmlAstBuilder.cs` | Visit `passesWhen` context |
| `PipelineRunner.cs` | Surface `PassesWhen` step name in `MissionResult` |
| `MissionResult.cs` | Add `PassesWhenStep` for observability |
| Tests | Parse test for `passes when`; runtime test that named step's fail propagates |
