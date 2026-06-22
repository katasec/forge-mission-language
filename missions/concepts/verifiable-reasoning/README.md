# Verifiable Step-by-Step Reasoning

## 1. Foundational concept

When an LLM solves a complex problem it produces an answer that *looks* right. But
"looks right" is not the same as "is right." The model generates the next token that
seems plausible — it cannot verify whether its reasoning steps are actually valid.

Verifiable reasoning separates *generation* from *verification*. The LLM proposes a
reasoning chain. A second, symbolic component checks whether that chain meets explicit
structural requirements — does it show its work? Does it reach a conclusion? Does it
follow the required format? If not, the LLM tries again with the specific failure stated.

This is how AlphaGeometry works. As DeepMind describe: the system pairs *"a neural
language model and a symbolic deduction engine"* — the LLM suggests geometric
constructions, the symbolic solver validates them rigorously. Neither alone could solve
olympiad-level problems. Together, AlphaGeometry 2 achieved gold-medal performance on
42/50 IMO geometry problems (DeepMind, 2024).

The insight generalises: any domain with explicit rules — accounting standards, legal
statutes, compliance requirements, structured reporting — can apply this pattern. The
symbolic layer enforces the rules; the LLM handles generation, reasoning, and explanation.

## 2. References

**Papers:**
- Trinh, T., et al. — "Solving Olympiad Geometry without Human Demonstrations" —
  *Nature*, DeepMind, 2024 —
  https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/
  LLM proposes geometric constructions; symbolic solver verifies; gold-medal geometry
  performance when combined.
- AlphaProof / AlphaGeometry 2 — DeepMind, 2025 —
  https://deepmind.google/discover/blog/ai-solves-imo-problems-at-silver-medal-level/
  42/50 IMO problems solved; the generate-verify loop is the core mechanism.
- Kosaraju, V., et al. — "RLSF: Fine-tuning LLMs via Symbolic Feedback" — 2024 —
  https://arxiv.org/pdf/2405.16661
  Symbolic certificates as token-level feedback for step-by-step correction — the
  RLSF mechanism this mission implements.
- Neurosymbolic AI: Comparative Study — 2025 — https://www.arxiv.org/pdf/2508.03366

**Industry/blog:**
- DeepMind — AlphaGeometry blog —
  https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/
- Gary Marcus — on AlphaGeometry: *"exactly the neurosymbolic approach I've been arguing for"*

## 3. How MCL demonstrates this

```
// Verifiable Step-by-Step Reasoning
// AlphaGeometry (DeepMind, Nature 2024) + RLSF (2024)

let problem = "A project has three workstreams: Engineering (must get at
least 40% of the budget), Design (must get at least 15%), and Research
(must get at least 10%). The total budget is fixed at 100 units. Allocate
the budget, show your reasoning step by step, and explain the rationale
for each allocation decision."

mission VerifiableReasoning(problem) loop(3) = {
    ProblemFramer
    -> ReasoningProposer
    -> StepVerifier
    -> ConclusionWriter
}

output(VerifiableReasoning)
```

`ProblemFramer` parses the problem once. `ReasoningProposer` generates a
numbered chain. `StepVerifier` (`kind: rule`) checks it deterministically —
if the chain lacks `"Step 1"`, `"Answer:"`, or enough sentences, the loop
retries with the exact failure named in `{{feedback}}`. `ConclusionWriter`
only runs after the chain passes — the stakeholder output is guaranteed to
have come from a verified chain.

### Example A — AlphaGeometry's generate-verify mechanism

AlphaGeometry pairs a neural LLM with a symbolic verifier. This mission implements
the same pattern in a general reasoning domain:

```
ProblemFramer → ReasoningProposer → StepVerifier (kind: rule) → ConclusionWriter
```

The `StepVerifier` applies symbolic checks to the reasoning chain:
```
check: contains "Answer:" and sentence_count >= 5 and contains "Step 1"
```

These rules are deterministic — they check verifiable structural properties of the
reasoning chain, not its content. If the chain fails, `onFail` names exactly what is
missing, and the `ReasoningProposer` retries with that specific feedback.

The mission uses `loop(3)`: if the StepVerifier fails, the pipeline restarts from
`ProblemFramer`. The Drafter sees `{{feedback}}` from the last failed verification
and corrects the specific structural issue. The `ConclusionWriter` only runs after
the chain passes verification — the stakeholder-facing output is guaranteed to have
come from a verified chain.

**What MCL adds:** the propose → verify → loop structure is entirely declarative.
Changing the domain means changing the `StepVerifier` rules in `expert.md`. No code
changes. The structure of the mission file makes the generate/verify split visible.

### Example B — compliance-grounded reasoning

Replace the `StepVerifier` check with domain rules:
- Accounting: `contains "balance" and contains "total" and sentence_count >= 6`
- Legal analysis: `contains "statute" and contains "precedent" and word_count >= 300`
- Engineering sign-off: `contains "assumption" and contains "margin" and markdown_has_heading`

The compliance officer writes the check. The runtime enforces it. The LLM generates
reasoning that satisfies it.

## 4. Why this is normally hard

Without MCL, verifiable reasoning requires:
- An LLM API client for the ProblemFramer and ReasoningProposer calls
- A verification module (custom code, or a symbolic solver like SymPy or Z3)
- Orchestration code to call generation, pass output to verifier, parse the result,
  decide whether to retry, format the retry prompt
- Integration between LLM output (natural language) and the verifier's input format
- Loop management, error handling, logging

For serious symbolic verification (mathematical proof checking, formal logic), this
requires fluency in Lean, Coq, Z3, or SymPy — tools that take months to learn.

The typical person who knows the domain rules — the accountant, the compliance officer,
the engineer — cannot build this system. They need a developer who understands both
the domain and the tooling.

**With MCL:**

```
kind: rule
check: contains "Answer:" and sentence_count >= 5 and contains "Step 1"
onFail: Show numbered steps, at least 5 sentences of reasoning, and end with "Answer:".
```

The domain expert writes the check expression. The runtime handles the loop, feedback,
and retry. The generate-verify separation is visible in the mission file — anyone
reading it can see exactly what is being verified and why.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission VerifiableReasoning
```

To change the domain, edit `let problem` in `mission.mcl` and update the `check`
and `onFail` in `experts/StepVerifier/expert.md`.
