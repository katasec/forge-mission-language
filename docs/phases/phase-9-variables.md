# Phase 9 — Variables and Functional Parameters

## Goal

Extend the FML grammar and runtime to support `let` bindings, mission parameters, and per-step
`with` clauses. The context carrier between experts changes from a plain `string` to a
`Dictionary<string, object>` — the same "bag" pattern as OWIN's `AppFunc` and Go's
`context.Context`.

## Completion condition

The extended `build-operator` example below parses, validates, and runs end-to-end:

```fsharp
let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }
```

Expert system prompts use `{{goal}}`, `{{persona}}`, `{{style}}` placeholders that are interpolated
at runtime from the context bag before each step runs.

## Grammar changes (delta from Phase 8 `Fml.g4`)

```antlr
program    : (letBinding | declaration)* EOF ;

letBinding : 'let' LOWER_ID '=' STRING ;

mission    : 'mission' UPPER_ID params? '=' pipeline ;
expert     : 'expert' UPPER_ID params? '=' pipeline ;

params     : '(' LOWER_ID (',' LOWER_ID)* ')' ;

pipeline   : step ('|>' step)* ;
step       : UPPER_ID withClause? ;

withClause : 'with' '{' binding (',' binding)* '}' ;
binding    : LOWER_ID '=' value ;
value      : STRING | LOWER_ID ;

LOWER_ID   : [a-z][a-zA-Z0-9]* ;
STRING     : '"' (~["\r\n])* '"' ;
```

## AST changes

- New `LetBinding(string Name, string Value)` node
- `Program` gains `IReadOnlyList<LetBinding> Bindings`
- `MissionDeclaration` gains `IReadOnlyList<string> Params`
- `Pipeline.Steps` changes from `IReadOnlyList<string>` to `IReadOnlyList<Step>`
- New `Step(string ExpertName, IReadOnlyList<Binding> With)` — `With` is empty list when no clause
- New `Binding(string Key, string Value)` — value is either a string literal or a variable reference

## Runtime changes

The context carrier changes from `string` to `Dictionary<string, object>`:

```csharp
// seeded at mission start
var context = new Dictionary<string, object>();
foreach (var binding in ast.Bindings)
    context[binding.Name] = binding.Value;

// per step: merge with-clause bindings, interpolate, run
foreach (var step in flattenedSteps)
{
    foreach (var b in step.With)
        context[b.Key] = Resolve(b.Value, context);

    var prompt = Interpolate(expert.SystemPrompt, context);
    var result = await runner.RunAsync(expert with { SystemPrompt = prompt }, (string)context["output"], ct);
    context["output"] = result;
}
```

`Interpolate` replaces `{{key}}` occurrences in system prompts with values from the context bag.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Extend `Fml.g4` with `let`, params, `with` clause, string literals, `LOWER_ID` | Not Started |
| 2 | Extend `FmlAstBuilder` — new AST nodes for `LetBinding`, `Step`, `Binding`; updated `Program` and `MissionDeclaration` | Not Started |
| 3 | Update `ExpertLoader.Validate` — accept missions with params without treating param names as missing experts | Not Started |
| 4 | Change runtime context carrier from `string` to `Dictionary<string, object>` | Not Started |
| 5 | Implement `Interpolate(string template, Dictionary<string, object> ctx)` — `{{key}}` substitution | Not Started |
| 6 | Update `PipelineRunner` — seed context from `let` bindings, merge `with` bindings per step, interpolate before each expert call | Not Started |
| 7 | Update `IExpertRunner.RunAsync` signature — accept context bag, not raw string | Not Started |
| 8 | Update `MafExpertRunner` — extract `output` from context bag as the user message | Not Started |
| 9 | Update CLI `fml run` — add `--var key=value` flag to inject values at call time (overrides `let` bindings) | Not Started |
| 10 | Update `examples/build-operator/mission.fml` to use `let` bindings, params, and `with` clause | Not Started |
| 11 | Update expert markdown files to use `{{goal}}` and `{{persona}}` placeholders | Not Started |
| 12 | Parser tests — `let` bindings parse correctly; params round-trip; `with` clause produces correct AST | Not Started |
| 13 | Runtime tests — interpolation; context seeding; `with` clause overrides; `--var` flag overrides `let` | Not Started |
| 14 | Integration test — end-to-end run of extended `build-operator` example | Not Started |

## Notes

- `IExpertRunner` signature change is a breaking change to the interface — `StubExpertRunner` in
  tests must be updated at the same time
- Variable resolution order (lowest to highest precedence): `let` binding → mission param binding →
  `with` clause → `--var` CLI flag
- `{{key}}` with no matching context entry should warn, not throw — expert may intentionally leave a
  placeholder for a prior step to fill
- The context bag is the OWIN `AppFunc` analogy: each expert reads what it needs and the `output`
  key carries the chained result forward
