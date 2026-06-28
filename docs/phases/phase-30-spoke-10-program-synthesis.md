# Phase 30 — Spoke 10: Program Synthesis (Dynamic Mission Generation)

> **Status: Design / Not started**
> **Depends on:** Phase 32 (kind:exec), Phase 35 (ForgeUI)
> **Motivation:** The month-vs-strawberry problem — static missions can't verify
> arbitrary questions. A meta-mission that writes its own verifier closes this gap.

---

## 1. Foundational concept

Large language models can write code. But when an LLM writes code that is then
*executed deterministically* to verify the LLM's own answer, something important
happens: the system becomes self-correcting without requiring a human to pre-program
every possible check.

**Program synthesis** is the automated generation of executable programs from a
specification. In the MCL context, this means: given a user's question, generate
a `verify.py` script tailored to that specific question, then execute it as a
`kind: exec` judge.

This closes the gap between static missions (one verifier per domain) and
general-purpose verification (one meta-mission for any factual question).

---

## 2. References

- **AlphaCode** (Li et al., DeepMind, 2022) — LLM-generated code as problem-solving.
- **Self-Debugging** (Chen et al., 2023) — LLMs fix their own code using execution feedback.
- **Program-Aided Language Models (PAL)** (Gao et al., 2022) — LLM writes Python, Python executes, result feeds back.
- **Marcus & Belle, AAAI 2025** — symbolic grounding as the necessary complement to neural generation.

---

## 3. How MCL demonstrates this

### The problem (month vs. strawberry)

A static `hallucination-guard` mission hardcodes `verify.py` to check month names
for the letter X. It correctly catches "February" but cannot verify "how many R's
are in strawberry" — the verifier is wrong for the question.

### Example A — Dynamic verifier synthesis

```
mission DynamicGuard(goal) = {
    MissionPlanner    ← kind:llm — reads goal, emits JSON with fields:
                        { "mcl": "...", "verifier": "python code..." }
    -> MissionRunner  ← kind:exec — writes verifier.py, runs forge run,
                        returns pass/fail + reason
}
```

For "how many R's in strawberry?":
- `MissionPlanner` generates: `assert "strawberry".count('r') == int(answer)`
- `MissionRunner` executes it against the Answerer's output
- If Answerer says "2", verifier catches it (correct is 3), loop retries

For "which month has X in the middle?":
- `MissionPlanner` generates: month name list check
- Same runner, different verifier, same mission

**One mission handles any factual question.**

### Example B — Financial compliance

A compliance officer asks "does our policy cover data retention for EU customers?"
- `MissionPlanner` generates a verifier that checks the answer against known
  GDPR article numbers (symbolic ground truth)
- Static missions would need one per regulation; DynamicGuard needs one total

---

## 4. Why this is normally hard

Without MCL:
- You need a framework that can: call an LLM, parse its output as code, write it
  to disk, execute it safely, capture stdout/stderr, feed results back into the
  next LLM call, and loop if it fails
- That's ~200 lines of Python orchestration + sandboxing + error handling
- The structure of the retry loop is buried in code, not auditable

With MCL:
```
mission DynamicGuard(goal) loop(3) = {
    MissionPlanner -> MissionRunner
}
```

The loop, the retry, the judge pattern — all declared. The generated verifier is
the only moving part, and it's captured in the trace for audit.

---

## 5. Design questions (to resolve before implementation)

1. **Output contract for MissionPlanner** — JSON with `verifier` (Python string)
   and optionally `mcl` (if a full sub-mission is generated). What schema?

2. **MissionRunner mechanics** — writes `verifier.py` to a temp dir, runs it with
   the Answerer's output as stdin, captures exit code + stdout as pass/fail.

3. **Safety** — generated code runs as `kind: exec`. Phase 32's wasm/hyperlight
   sandboxing backend is the right answer. Until then: subprocess with timeout +
   no network access flag.

4. **Ephemeral vs persistent** — does the generated mission get saved? Useful for
   the 20-sample eval (collect all generated verifiers as a corpus).

---

## 6. Connection to the 20-sample eval

If DynamicGuard works, it generates per-question verifiers automatically.
The 20-sample eval (see `project_next_todos`) no longer requires 20 hand-coded
missions — DynamicGuard produces them at runtime. The eval becomes:

1. Run 20 factual questions through DynamicGuard
2. Run the same 20 through vanilla (no verifier)
3. Count: how many times did DynamicGuard catch a wrong answer that vanilla passed?

That delta is the empirical neurosymbolic proof.
