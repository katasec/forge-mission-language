# Phase 30 Spoke 7 — Hallucination Reduction via Symbolic Grounding

> **Status: Brainstorm**
> **Papers:** RLSF (2024); Marcus neurosymbolic thesis; Neurosymbolic AI comparative study (2025)
> **Industry:** Gary Marcus (Substack); IBM neurosymbolic AI overview

---

## 1. Foundational concept

LLMs are fluent but unreliable. When asked a factual question or to produce a
structured output, they frequently generate plausible-sounding but incorrect answers —
a phenomenon called hallucination. The model has no internal mechanism to check whether
its output satisfies a constraint; it can only predict what *looks* like the right answer.

The neurosymbolic fix is simple in principle: add a symbolic layer that checks the LLM's
output against explicit rules before accepting it. If the check fails, send the failure
reason back to the LLM and try again. This is the "push determinism left, LLM judgment
right" principle — use a fast, guaranteed-correct rule to catch what the LLM got wrong,
and use the LLM only for what rules can't do (generation, reasoning, nuance).

This is not a new idea — control systems in engineering have worked this way for decades.
What is new is applying it to LLM pipelines in a declarative, readable form.

---

## 2. References

**Papers:**
- [RLSF: Fine-tuning LLMs via Symbolic Feedback (2024)](https://arxiv.org/pdf/2405.16661) —
  demonstrates that symbolic feedback (rule-based certificates) produces more reliable
  LLM outputs than human feedback alone
- [Neurosymbolic AI: Comparative Study of Logical Reasoning (2025)](https://www.arxiv.org/pdf/2508.03366) —
  hybrid LLM + symbolic solver is more promising for logical reasoning; reasoning chain
  is more interpretable; retains LLM advantages
- Gary Marcus & Vaishak Belle — [The Future Is Neuro-Symbolic, AAAI 2025](https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf)

**Industry/blog:**
- [IBM: What Are Compound AI Systems?](https://www.ibm.com/think/topics/compound-ai-systems) —
  "architectures using symbolic solvers to enforce factual grounding and robust reasoning"
- Gary Marcus, Substack — "The biggest advance since the LLM" (on Claude Code as symbolic harness)

---

## 3. How MCL demonstrates this

```
LLMDrafter
-> FactChecker        (kind: rule)
-> loop 3 { LLMDrafter } with {{feedback}}
```

- `LLMDrafter` — generates an answer or structured output
- `FactChecker` — `kind: rule`; applies explicit deterministic checks (e.g. word count,
  JSON parseable, contains required fields, value within range); writes `{{feedback}}`
  with the specific violation if it fails; passes if all checks pass
- `loop 3` — if `FactChecker` fails, reruns `LLMDrafter` with the failure reason
  injected as `{{feedback}}`; exits early when the check passes

The rule expert is the symbolic grounding layer. It does not guess — it checks.
The loop is the correction mechanism. The whole thing is 5 lines of MCL.

**What MCL adds:** `kind: rule` and the loop/feedback mechanism compose cleanly with
any LLM expert. The symbolic check is not bolted on outside the system — it is a
first-class pipeline participant. Changing the rules means editing an `expert.md` file,
not modifying orchestration code.

---

## 4. Why this is normally hard

Without MCL, implementing this requires:

- Python code to call the LLM API
- A separate validation function (or library) for each rule
- Manual retry logic: catch the failure, format a new prompt with the error, re-call the API
- State management: tracking iteration count, storing prior attempts
- Error handling for API failures vs. validation failures

A typical LangChain implementation of this pattern spans 50–100 lines across multiple
files and requires understanding of Python async, prompt template management, and
chain composition. A developer familiar with these tools can build it; a finance
professional, a doctor, or an operations manager cannot — even if they understand
exactly what they want the system to do.

With MCL:

```
LLMDrafter
-> FactChecker (kind: rule, check: "json_parseable AND contains_key(score)")
-> loop 3 { LLMDrafter }
```

The person who designed the rule IS the person who can write this. The concept and
the implementation are the same artifact.
