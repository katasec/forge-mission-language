# Phase 25 — Spoke 1: Grammar

## Status: Todo

## Summary of changes

This spoke updates the MCL grammar and AST to reflect all design decisions made in the
Phase 25 pre-flight. These are breaking changes to the grammar but pre-1.0, so no migration
compatibility shim is needed. `forge migrate` (future) will handle any existing `.mcl` files.

| Change | Old syntax | New syntax |
|--------|-----------|-----------|
| Sequence operator | `\|>` | `->` |
| Mission body | `mission X = \n    step` | `mission X = { step }` |
| Step context | `with { key = value }` | `(key: value)` |
| Provider profile | *(reserved let bindings in mission)* | `using <profile>` |
| Conditional guard | `when { mode = "x" }` | `when(mode: "x")` |
| Default branch | *(none)* | `when(else)` |
| Loop | `loop N` | `loop(N)` |
| Parallel block | `parallel { }` | `parallel { }` *(unchanged)* |
| Expert declarations | `expert X = from … version …` | Removed — use `forge.toml` |

---

## Change 1 — Replace `|>` with `->`

`->` means "passes to" — neutral, directional, no prior art baggage. Developers read it
correctly on first encounter without knowing MCL.

**Before:**
```fsharp
mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

**After:**
```fsharp
mission BuildOperatorDesign(goal, persona) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> PrincipalReviewer
}
```

---

## Change 2 — Braces on mission body

Every scope has an explicit `{ }`. Mission body, `parallel {}`, `debate {}` — one rule,
no exceptions. Not whitespace-sensitive. The `=` is the assignment operator ("is defined
as"); the `{ }` is the scope delimiter.

**Before:**
```fsharp
mission X(goal) =
    StepA
    -> StepB
```

**After:**
```fsharp
mission X(goal) = {
    StepA
    -> StepB
}
```

---

## Change 3 — Named parameters with `:` — remove `with {}`

`:` is the universal named parameter separator throughout MCL. `with { key = value }` is
removed — it used a different separator (`=`) and a separate keyword. The replacement is
a context clause on the step itself.

**Before:**
```fsharp
-> PrincipalReviewer with { style = "terse ADR" }
-> Synthesiser with { format = "ADR" }
```

**After:**
```fsharp
-> PrincipalReviewer(style: "terse ADR")
-> Synthesiser(format: "ADR")
```

`:` is the separator throughout: step context `(source: codebase)`, execution config
`debate(rounds: 3)`, guard conditions `when(mode: "design")`.

---

## Change 4 — `using` for provider profile selection

Provider profile selection moves from reserved `let` bindings in `mission.mcl` to an
explicit per-step `using <profile>` clause. Infrastructure and domain are now orthogonal.

**Before:** *(provider embedded as let bindings, per-step override impossible)*
```fsharp
let provider = "anthropic"
let model = "claude-opus-4-8"
```

**After:**
```fsharp
mission BuildOperatorDesign(goal) = {
    KubernetesArchitect using architect
    -> SecurityArchitect
    -> PrincipalReviewer(style: "terse") using fast
}
```

`using <identifier>` selects a named profile from `forge.toml`. All steps without
`using` use the `default` profile. `using` and `()` context are composable —
both, either, or neither may appear on any step.

---

## Change 5 — `when()` conditional step guard

`when()` is a new grammar primitive — a step guard that makes a step conditional on a
context bag value set by a prior step. This is the language primitive that enables the
classifier-router pattern.

```fsharp
mission HandleRequest(input) = {
    Classifier
    -> Architect when(mode: "design")
    -> Developer when(mode: "task")
    -> Reviewer  when(mode: "review")
    -> Planner   when(else)
}
```

**Semantics (Phase 25):**
- `when(key: value)` — exact string match against context bag
- `when(else)` — explicit default; hard error at runtime if nothing matched and no `else`
- Unmatched steps skip silently; logged at `--verbose`

`WhenExpression` is an abstract AST node with two concrete subtypes:
- `StringEqualsExpression(string Key, string Value)` — Phase 25
- `ElseExpression` — Phase 25

New expression types (`GreaterThanExpression`, `ContainsExpression`, etc.) are additive
extensions in future phases. The seam is intentional — no runtime change required, only
new parser productions and AST subtypes.

---

## Change 6 — `loop(N)` replaces `loop N`

`loop(N)` is syntactically consistent with all other call-like constructs in MCL. Same
semantics as the old `loop N` — reruns the pipeline up to N times until the last step
emits `pass` in its `StepEnvelope`.

**Before:**
```fsharp
mission BuildOperatorDesign(goal) loop 3 = ...
```

**After:**
```fsharp
mission BuildOperatorDesign(goal) loop(3) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> QualityJudge
}
```

Platform-managed feedback injection: the runtime prepends a structured critique
(Constitutional AI model) to the first expert's context on each retry. `{{feedback}}`
is removed as a developer-facing variable — no expert prompt needs to reference it.

Reserved variables unchanged: `{{attempt}}`, `{{max_loops}}`.

---

## Change 7 — `parallel {}` block *(unchanged structure, updated context)*

Syntax unchanged from the design decision — `parallel { }` with `->` before and after.
Per-step `using` and `()` context clauses are now valid inside `parallel {}` blocks.

```fsharp
mission Analysis(input) = {
    DataExtractor
    -> parallel {
        Summariser
        FactChecker
        Critic using fast
    }
    -> Synthesiser
}
```

Each parallel expert's output is available downstream as `{{ExpertName}}`.
Failure: fail-fast (Rob Pike errgroup model) — any failure cancels in-flight experts
via context propagation. No configurable failure mode.

---

## Change 8 — Remove OCI expert declaration from `.mcl`

`expert … from … version …` declarations move entirely to `forge.toml`. The grammar
no longer accepts them in `.mcl` files.

**Before (in mission.mcl):**
```fsharp
expert KubernetesArchitect =
    from "ghcr.io/katasec/forge-kubernetes-architect"
    version "0.1.0"
```

**After:** not valid in `.mcl`. Declared in `forge.toml` instead.

Local experts (directory-based) remain valid — resolved by name without any declaration.

---

## Change 9 — Mission composition (step resolution extended)

A mission can be used as a step in another mission's pipeline. The resolution order
for any `UPPER_ID` encountered in a pipeline:

```
1. ./experts/<Name>/expert.md     ← leaf expert: single LLM call
2. ./missions/<Name>.mcl          ← composite: sub-pipeline
3. ~/.forge/cache/<Name>/         ← OCI (expert or mission)
4. forge stdlib                   ← built-in experts only
5. error[R002]: not found
```

Explicit parameter binding at the call site:

```fsharp
mission FullDevelopmentCycle(goal) = {
    RequirementsAnalyst
    -> CodeReview(codebase: goal)      // CodeReview is itself a mission
    -> DeploymentPlanner
}
```

No context inheritance — inner mission context is isolated. Caller binds parameters
explicitly; inner mission cannot access the outer context bag.

---

## ANTLR grammar changes

Full grammar: see [`docs/design/language.md`](../design/language.md#grammar).

Diff from current grammar:

```
Remove:
  PIPE     : '|>' ;
  expertDecl, expertSource, FROM, VERSION tokens and productions
  withClause rule
  'loop' NUMBER (bare integer after loop keyword)

Add:
  ARROW    : '->' ;          (replaces PIPE)
  LBRACE, RBRACE             (mission body now explicit)
  loopClause : 'loop' '(' NUMBER ')' ;
  contextClause : '(' binding (',' binding)* ')' ;
  binding   : LOWER_ID ':' value ;
  usingClause : 'using' LOWER_ID ;
  whenClause : 'when' '(' whenExpr ')' ;
  whenExpr   : LOWER_ID ':' STRING | 'else' ;
  USING, WHEN, ELSE, ROUNDS keywords

Update:
  mission production: add optional loopClause, mandate '{' '}' around pipeline
  pipeline: uses ARROW not PIPE
  step: add optional contextClause, usingClause, whenClause
  parallelBlock: steps inside may have contextClause and usingClause
```

After grammar changes, regenerate the parser:
```bash
java -jar /tmp/antlr4-4.13.1-complete.jar -Dlanguage=CSharp -package ForgeMission.Core.Parser \
     -visitor -o src/ForgeMission.Core/Parser/Generated \
     src/ForgeMission.Core/Parser/MclGrammar.g4
```

---

## AST changes

### Removed
- `ExpertSource` node — `from`/`version` on expert declarations
- `WithClause` node — replaced by `ContextClause`

### Added
- `ContextClause` node — list of `Binding(string Key, ContextValue Value)`
- `UsingClause` node — `string ProfileName`
- `WhenClause` node — holds a `WhenExpression`
- `WhenExpression` — abstract base
  - `StringEqualsExpression(string Key, string Value)` — Phase 25
  - `ElseExpression` — Phase 25
- `LoopClause` node — `int MaxAttempts`

### Updated
- `MissionDeclaration` — gains optional `LoopClause`, body now explicitly delimited
- `Step` — gains optional `ContextClause`, `UsingClause`, `WhenClause`
- `ParallelBlock` — steps inside may have `ContextClause` and `UsingClause`

---

## Test gate

- All existing parser tests pass with `->` substituted for `|>`
- All existing `loop N` tests pass with `loop(N)` form
- All existing `with { }` tests pass with `(key: value)` context clause
- New tests: `when()` parsing, `when(else)` parsing, `using` parsing
- New tests: mission composition — `./missions/` step resolution
- New negative tests: `from`/`version` in `.mcl` is parse error; `with {}` is parse error
- Source positions on all new AST nodes (prerequisite for Phase 26 error underlines)
