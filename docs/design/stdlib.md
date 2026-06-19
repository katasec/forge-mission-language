# MCL Standard Library — Definition and Gates

## What the standard library is

MCL ships with a small set of built-in experts embedded in the `forge` binary. These are
the structural primitives of the language — the `fmt`, `io`, and `sync` of MCL. They
require no declaration in `forge.toml`, no entry in `mcl.lock`, and no network call.
They are always available, including offline.

Everything else — domain experts, industry-specific reasoning, community packages — comes
from the OCI registry and is declared explicitly in `forge.toml`.

## The four gates

Every expert proposed for the standard library must pass all four gates. Failing any one
means the expert belongs in the OCI registry, not the binary.

### Gate 1 — Pipeline mechanics, not domain knowledge

The expert operates on *how the pipeline runs*, not *what domain it reasons about*. It
has no knowledge of Kubernetes, security, finance, medicine, law, or any specific field.
Its system prompt must be meaningful and correct for every possible mission, regardless
of subject matter.

**Disqualified by Gate 1:** a `Critic` expert — "find flaws" in a security design is
fundamentally different from "find flaws" in a business strategy. The system prompt
cannot be domain-agnostic without becoming useless.

### Gate 2 — Universal across all possible missions

The expert is useful regardless of domain, industry, use case, or pipeline shape. If a
reasonable mission exists where this expert would never be needed, it is not universal.

**Disqualified by Gate 2:** a `CodeReviewer` — only relevant to software missions.

### Gate 3 — Canonical implementation required

If every team writes their own version, the ecosystem suffers from incompatible
behaviour. The expert's behaviour must be load-bearing for a language primitive —
specifically: if correct operation of `parallel {}`, `loop(N)`, or `when()` depends on
a consistent implementation, a canonical version is required.

This is Russ Cox's test from Go: *"We accept something into stdlib when the alternative
is everyone writing their own incompatible version."*

**Disqualified by Gate 3:** a general `Summariser` — domain summaries differ by field,
and incompatible implementations do not hurt the language's core primitives.

### Gate 4 — Stable enough to freeze forever

Standard library experts fall under the same backwards compatibility guarantee as the
MCL grammar. Once included, their behaviour cannot be changed in a breaking way without
a major version bump. If we cannot confidently commit to freezing the expert's behaviour
permanently, it does not belong in the binary.

This is the highest-cost gate. Go has rejected stdlib proposals it liked because the
team was not confident the API could be frozen. The cost of inclusion is a permanent
promise.

---

## Current standard library

| Expert | Role | Load-bearing for |
|--------|------|-----------------|
| `ContextSummariser` | Compress accumulated `{{output}}` before a step to prevent context window overflow | Long pipelines — without a canonical compressor, every team implements differently |
| `QualityJudge` | Assess step output quality; returns `pass` or `fail` with structured reason | `loop(N)` convergence — the loop primitive depends on a consistent pass/fail signal |
| `Synthesiser` | Merge outputs from a `parallel {}` block into a single coherent result | `parallel {}` fan-in — the block primitive needs a canonical merge behaviour |
| `Classifier` | Inspect input and emit a structured routing signal into the context bag | `when()` routing — the conditional step primitive depends on consistent classification; incompatible implementations produce incompatible routing signals |

## Applying the gates — worked examples

### `Critic` — rejected

| Gate | Result | Reason |
|------|--------|--------|
| Gate 1 — mechanics only | ✗ | "Find flaws" is domain reasoning. Security flaws ≠ prose flaws ≠ logical flaws. |
| Gate 2 — universal | ~ | Arguable, but fails Gate 1 |
| Gate 3 — canonical needed | ✗ | Different domains want different critics — incompatibility is desirable, not harmful |
| Gate 4 — freezable | ✗ | Cannot commit to one "find flaws" prompt forever across all domains |

**Verdict: OCI.** Community can publish `ghcr.io/katasec/forge-critic-security`, `forge-critic-prose`, etc.

### `Synthesiser` — accepted

| Gate | Result | Reason |
|------|--------|--------|
| Gate 1 — mechanics only | ✓ | Merges pipeline outputs — no domain knowledge required |
| Gate 2 — universal | ✓ | Every mission using `parallel {}` needs a merge step |
| Gate 3 — canonical needed | ✓ | `parallel {}` fan-in semantics depend on a consistent merge; incompatible versions break pipeline composition |
| Gate 4 — freezable | ✓ | "Merge these outputs coherently" is stable enough to commit to |

**Verdict: Stdlib.**

---

## Distribution and resolution

Standard library experts are embedded as resources in the `forge` binary. They are
resolved last — local experts and OCI cache always take precedence:

```
1. ./experts/<Name>/expert.md          ← local, always wins
2. ~/.forge/experts/<Name>/            ← OCI cache (declared in forge.toml)
3. forge built-in standard library     ← implicit, no declaration needed
4. error[R002]: expert not found
```

A local `ContextSummariser` overrides the built-in — the author's version wins. This is
intentional: mission authors can refine stdlib behaviour for their domain without forking
the binary.

## Updates

Built-in experts are updated with the forge binary. Adding a new stdlib expert is a
`mcl` minor version bump (`"1.0"` → `"1.1"`). Changing the behaviour of an existing
stdlib expert in a breaking way requires a major version bump (`"1.0"` → `"2.0"`).

Community and domain experts are updated via `forge update` which bumps `mcl.lock`.

## The one-sentence rule

> An expert belongs in the standard library if and only if it operates on pipeline
> mechanics rather than domain knowledge, is useful across every possible mission,
> is load-bearing for a language primitive, and we can commit to freezing its behaviour
> forever.

When a proposal arrives — from the community, a contributor, or the language authors —
run it through the four gates. If it fails any one, it goes to OCI.
