# Hallucination Reduction via Symbolic Grounding

## 1. Foundational concept

LLMs are fluent but unreliable. When asked to produce structured output — a formatted
report, a JSON object, a numbered list — they frequently fail in subtle ways: missing a
required section, producing prose when JSON was expected, or ignoring format constraints.
The model cannot verify its own output against external rules; it can only generate what
*looks* correct.

The neurosymbolic fix is to add a symbolic layer that checks the LLM's output against
explicit deterministic rules before accepting it. If the check fails, the specific
violation is sent back to the LLM so it can correct the exact problem.

As Marcus & Belle (AAAI 2025) frame it: *"The future is neuro-symbolic"* — neural
generation for fluency and content, symbolic rules for verifiability and constraint
enforcement. This principle is applied here in its simplest form: an LLM that cannot
reliably produce a well-structured output on the first attempt becomes reliable when
paired with a deterministic checker that catches and explains structural failures.

## 2. References

**Papers:**
- RLSF: Kosaraju, V., et al. — "RLSF: Fine-tuning LLMs via Symbolic Feedback" — 2024 —
  https://arxiv.org/pdf/2405.16661
  Demonstrates that symbolic feedback (rule-based certificates) produces more reliable
  LLM outputs than human feedback alone. The symbolic rule IS the correctness signal.
- Marcus, G. & Belle, V. — "The Future Is Neuro-Symbolic" — AAAI 2025 —
  https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf
  *"The biggest advance since the LLM"* requires symbolic supplementation. Pure neural
  systems hit reliability walls that symbolic checks resolve.
- Neurosymbolic AI: Comparative Study of Logical Reasoning — 2025 —
  https://www.arxiv.org/pdf/2508.03366
  Hybrid LLM + symbolic systems are more reliable for structured reasoning tasks;
  the reasoning chain is more interpretable; LLM advantages are retained.

**Industry/blog:**
- IBM — "What Are Compound AI Systems?" —
  https://www.ibm.com/think/topics/compound-ai-systems
  *"Architectures using symbolic solvers to enforce factual grounding and robust reasoning"*
  as a defining characteristic of production AI systems.
- Gary Marcus, Substack — on Claude Code as symbolic harness:
  *"Claude Code is an immense neurosymbolic effort to ward off the failure of pure LLMs."*

## 3. How MCL demonstrates this

```
// Hallucination Reduction via Symbolic Grounding
// RLSF (2024) + Marcus & Belle, AAAI 2025

let topic = "the key technical differences between SQL and NoSQL databases,
including when to choose each"

mission HallucinationReduction(topic) loop(3) = {
    LLMDrafter
    -> FactChecker
}

output(HallucinationReduction)
```

`FactChecker` is a `kind: rule` expert — no LLM call, no network, microsecond
execution. Its check expression:

```
check: 'markdown_has_heading and word_count >= 150 and contains "conclusion"'
```

When any clause fails, `onFail` names the specific missing requirement and the
loop retries. The LLM is only responsible for generation; the rule is responsible
for correctness. Neither alone is as reliable as both together.

### Example A — the RLSF symbolic feedback mechanism

RLSF (2024) demonstrates that symbolic feedback — a machine-verifiable rule that
certifies whether an output meets a constraint — is more reliable than human feedback
for improving LLM outputs. The FactChecker in this mission is that certificate:

The `FactChecker` (`kind: rule`) applies three deterministic checks simultaneously:
```
check: markdown_has_heading and word_count >= 150 and contains "conclusion"
```

These are symbolic rules — not estimates, not heuristics, not LLM opinions. They are
guaranteed to be correct. When a check fails, the `onFail` message names exactly what
was missing. The Drafter receives this specific feedback and knows exactly what to fix.

This is the RLSF mechanism: symbolic certificates as correctness signals. The LLM is
not asked to self-evaluate (which is unreliable) — it is told by a deterministic rule
what failed.

**What MCL adds:** `kind: rule` and the loop/feedback mechanism compose cleanly with
any LLM expert. The symbolic check is a first-class pipeline participant. Changing the
rules means editing `experts/FactChecker/expert.md` — not modifying orchestration code.

### Example B — domain-specific constraint enforcement

Replace the `check` expression with domain-specific rules:
- Legal document: `markdown_has_heading and word_count >= 500 and contains "WHEREAS"`
- API response format: `json_parseable and word_count >= 1`
- Compliance report: `contains "risk" and contains "recommendation" and word_count >= 200`

The person who knows the domain rules — the lawyer, the API designer, the compliance
officer — can write the `check` expression. No Python, no regex library, no custom
validator code.

## 4. Why this is normally hard

Without MCL, implementing symbolic grounding requires:
- A Python validation function for each rule
- Retry logic: call LLM, catch the failure, format a new prompt with the error reason,
  re-call the API, track iteration count
- State management: storing the failure message for injection into the next prompt
- Error handling distinguishing API failures from validation failures
- Separate code for the LLM call and the rule check — two different systems to maintain

A typical Python implementation of this pattern spans 50–100 lines across multiple
files. A developer who understands LangChain and Python async can build it. A compliance
officer who knows the output must contain "WHEREAS" and be at least 500 words cannot.

**With MCL:**

```
kind: rule
check: markdown_has_heading and word_count >= 150 and contains "conclusion"
onFail: Your briefing must have a markdown heading, at least 150 words, and the word "conclusion".
```

The person who knows the domain rules writes the check. The runtime handles the loop,
the feedback injection, and the retry. The concept and the implementation are the same
three-line file.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission HallucinationReduction
```

To enforce different structural constraints, edit the `check` and `onFail` fields in
`experts/FactChecker/expert.md`.
