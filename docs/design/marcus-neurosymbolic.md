# Gary Marcus — Neurosymbolic AI: Use Cases and PoC Guide

> Internal reference document. Source: Gary Marcus, "Neurosymbolic AI Use Cases from
> His Critiques of Pure LLMs, Superior Hybrid Solutions, and Pathways to Practical PoCs"
> (comprehensive guide synthesising his public responses on X/Substack and research papers).

---

## Marcus's core thesis

Gary Marcus (Professor Emeritus, NYU; founder of Geometric Intelligence / Robust.AI)
argues that LLMs are powerful statistical pattern matchers but lack innate structure for
compositionality, causal reasoning, abstraction, and reliable generalisation. They
hallucinate, fail on out-of-distribution inputs, and are poor sample-efficient learners.

The fix: **neurosymbolic AI** — integrating neural components (for perception, learning,
generation) with symbolic components (explicit knowledge, rules, logic, planning,
verifiability). Recent industry moves (tool use, code interpreters, formal methods,
harnesses) prove his point: pure LLMs hit a wall; symbolic supplementation is necessary.

His direct framing of Claude Code: *"Claude Code is an immense neurosymbolic effort to
ward off the failure of pure LLMs. Claude Code without the harness is just a hallucination
generator."*

---

## Use cases from his critiques

### 1. Coding and software engineering

**Problem:** Reliable, production-grade software requires compositionality, verifiability,
and handling edge cases — not just fluent generation.

**Marcus's critique of pure LLMs:** probabilistic "autocomplete," prone to hallucination,
lacks understanding of program semantics or long-term consequences.

**Neurosymbolic solution:** LLM for generation + symbolic harnesses (control flow, state
machines), tools/verifiers (compilers, linters, test runners, static analysers), structured
memory (knowledge graphs, project-specific rules).

**MCL mapping:** the `mission.mcl` file IS the harness. `kind: rule` is the verifier.
`loop` + judge is the correction loop.

---

### 2. Mathematical reasoning and theorem proving

**Problem:** Exact step-by-step logical deduction; proofs must be checkable.

**Marcus's critique:** LLMs mimic steps but hallucinate invalid proofs; they lack
mechanisms for rigorous verification or systematic proof-space search.

**Neurosymbolic solution:** LLM for candidate step generation + symbolic formal systems
(Lean, Coq) for verification and proof construction. Examples: AlphaProof, AlphaGeometry.

**MCL mapping:** Spoke 8 (verifiable step-by-step reasoning). `kind: rule` verifier per step.

---

### 3. Business / enterprise structured data

**Problem:** Querying and reasoning over relational databases, ERPs, knowledge graphs.
Requires joins, aggregations, constraint checking, compliance rules.

**Marcus's critique:** *"LLMs mastered language. But they don't understand the structured,
relational data that businesses actually run on."*

**Neurosymbolic solution:** NL interface (neural) over relational DB + symbolic query
planner, rule engine for business logic/compliance, verification against schemas.

**MCL mapping:** `kind: rule` for constraint checking; `kind: json_extract` for structured
data bridging; LLM experts for NL in/out.

---

### 4. Autonomous systems and real-world planning

**Problem:** Safe, reliable navigation in dynamic environments. Hard safety constraints.

**Neurosymbolic solution:** Neural perception + symbolic world models, planners, constraint
satisfaction for trajectory planning under rules (traffic laws, physics). LLMs for
high-level intent, not core control loops.

---

### 5. General reasoning, compositionality, hallucination reduction

**Problem:** Abstraction, causal inference, out-of-distribution generalisation.

**Marcus's critique:** *"Embeddings aren't enough. Sooner or later you are going to need
true compositionality."*

**Neurosymbolic solution:** Neural for perceptual/pattern components + symbolic for explicit
composition (graphs, rules, programs), causal models, search/planning, verification.

**MCL mapping:** Spoke 9 (compositionality). Spoke 7 (hallucination reduction).

---

## PoC criteria Marcus would validate

A strong PoC must be:
- **Hybrid by design** — explicit neural + symbolic with clear interfaces
- **Measurably reliable** — metrics for hallucination reduction, task success, verifiability
- **Domain-grounded** — structured or rule-rich areas (code, data, planning, monitoring)
- **Transparent** — explainable decisions via symbolic traces
- **Aligned** — addresses compositionality, world models, or efficiency

---

## MCL's neurosymbolic mapping

| Marcus's required layer | MCL primitive |
|---|---|
| Neural generation / fluency | `kind: llm` |
| Symbolic rules / deterministic verification | `kind: rule` |
| Embedded classical ML inference | `kind: onnx` |
| Neural → symbolic data bridge | `kind: json_extract` |
| Symbolic scaffolding / harness | the `mission.mcl` file |
| Verification + correction loop | `loop` + `role: judge` + `{{feedback}}` |

---

## Related sources

- [Marcus & Belle, "The Future Is Neuro-Symbolic," AAAI 2025](https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf)
- Gary Marcus, Substack (Marcus on AI) — "The biggest advance since the LLM" (on Claude Code)
- [BAIR Compound AI Systems (Zaharia et al., Feb 2024)](https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/)
- [Neurosymbolic AI Comparative Study (2025)](https://www.arxiv.org/pdf/2508.03366)
