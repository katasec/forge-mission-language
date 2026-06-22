# Phase 30 Spoke 8 — Verifiable Step-by-Step Reasoning

> **Status: Brainstorm**
> **Papers:** AlphaGeometry (DeepMind, 2024); AlphaProof (DeepMind, 2025); RLSF (2024)
> **Industry:** DeepMind blog on AlphaGeometry; Gary Marcus on hybrid provers

---

## 1. Foundational concept

When an LLM solves a complex problem — a math question, a legal analysis, a financial
projection — it produces an answer that looks right. But "looks right" is not the same
as "is right." The model cannot verify its own steps; it can only generate the next
token that seems plausible given the prior ones.

Verifiable reasoning separates *generation* from *verification*. The LLM proposes a
reasoning step. A second, symbolic component checks whether that step is valid — does
it follow from the prior step? Does it satisfy a constraint? Is the calculation correct?
If not, the LLM tries again with the failure clearly stated.

This is how AlphaGeometry works: the LLM suggests geometric constructions, the symbolic
solver validates them rigorously. Neither component alone could solve olympiad-level
problems. Together, AlphaGeometry 2 reached gold-medal performance (42/50 problems).

The insight generalises beyond mathematics: any domain with explicit rules (accounting
standards, legal statutes, engineering constraints, compliance requirements) can benefit
from this pattern.

---

## 2. References

**Papers:**
- [AlphaGeometry: An Olympiad-level AI system for geometry (DeepMind, Nature 2024)](https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/) —
  LLM proposes, symbolic solver verifies; neither alone reaches gold-medal level
- [AlphaProof / AlphaGeometry 2 (DeepMind, 2025)](https://deepmind.google/discover/blog/ai-solves-imo-problems-at-silver-medal-level/) —
  42/50 IMO problems; gold-medal geometry performance
- [RLSF: Fine-tuning LLMs via Symbolic Feedback (2024)](https://arxiv.org/pdf/2405.16661) —
  symbolic certificates as fine-grained token-level feedback for step-by-step correction
- [Neurosymbolic AI comparative study (2025)](https://www.arxiv.org/pdf/2508.03366)

**Industry/blog:**
- [DeepMind AlphaGeometry blog](https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/)
- Gary Marcus on AlphaGeometry: "exactly the neurosymbolic approach I've been arguing for"

---

## 3. How MCL demonstrates this

```
ProblemFramer
-> ReasoningProposer
-> StepVerifier        (kind: rule)
-> loop 3 { ReasoningProposer } with {{feedback}}
-> ConclusionWriter
```

- `ProblemFramer` — LLM expert; parses the problem, structures it into explicit sub-goals
- `ReasoningProposer` — LLM expert; proposes the next reasoning step given the current state
- `StepVerifier` — `kind: rule`; checks the proposed step against explicit constraints
  (e.g. arithmetic correctness, logical entailment, compliance rule); writes `{{feedback}}`
  with the specific failure if invalid
- `loop 3` — repeats the propose → verify cycle until the step passes or max iterations
- `ConclusionWriter` — LLM expert; writes the final answer given verified reasoning chain

**Demo domain:** A simpler, accessible domain than pure mathematics — e.g. a financial
calculation with explicit rules (must sum to 100%, no negative values, must cite a
source for each assumption) or a compliance checklist (each claim must map to a numbered
regulation). The symbolic verifier enforces the domain rules; the LLM handles generation
and explanation.

**What MCL adds:** the propose → verify → loop structure is entirely declarative.
Changing the domain means changing the `StepVerifier` rules in `expert.md`. No code
changes. The structure of the mission file makes the generate/verify split immediately
visible.

---

## 4. Why this is normally hard

Without MCL, verifiable reasoning requires:

- An LLM API client for generation
- A separate verification module (custom code, or a symbolic solver like SymPy, Z3, Lean)
- Orchestration code to call generation, pass output to verifier, parse the result,
  decide whether to retry, format the retry prompt
- Integration between the LLM's natural language output and the symbolic verifier's
  input format (parsing, structured extraction)
- Loop management, error handling, logging

Even for a simple domain like "verify the numbers add up," building this in Python with
LangChain requires understanding of chain composition, output parsers, and retry logic.
For a serious symbolic solver (SymPy, Z3), it requires fluency in those libraries too.

The typical person who *knows* the domain rules — the accountant, the compliance officer,
the engineer — cannot build this system. They need a developer, who then needs to
understand both the domain and the tooling.

With MCL, the accountant writes the rules in `StepVerifier/expert.md` in plain English
with a `check:` expression, and the verification loop is built into the language.
The domain expert IS the implementer.
