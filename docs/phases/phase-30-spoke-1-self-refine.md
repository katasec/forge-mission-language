# Phase 30 Spoke 1 — Self-Refine

> **Status: Brainstorm**
> **Paper:** [Self-Refine: Iterative Refinement with Self-Feedback (Madaan et al., NeurIPS 2023)](https://arxiv.org/abs/2303.17651)

---

## Paper summary

Generate an initial output, critique it, refine using the critique — iteratively.
No fine-tuning required. Showed ~20% absolute improvement across 7 tasks
(GPT-3.5, ChatGPT, GPT-4): dialog, code, math reasoning, sentiment reversal, and more.

Key finding: the critique must be specific and actionable for refinement to improve output.
Generic "make it better" critique produces little gain.

---

## MCL expression

```
Drafter → Judge → loop 3 { Drafter } with {{feedback}}
```

- `Drafter` — produces the initial output
- `Judge` — critiques against an explicit rubric; writes failure reason to `{{feedback}}`; passes when good enough
- `loop 3` — reruns `Drafter` up to 3 times, injecting `{{feedback}}` each time
- If `Judge` passes before iteration 3, the loop exits early

The `{{feedback}}` variable is the exact analogue of Self-Refine's critique signal.
MCL makes the loop boundary and feedback injection visible in the mission syntax —
not buried in orchestration code.

**What MCL adds over the paper:** the Drafter and Judge have separate, role-optimised
system prompts. Self-Refine uses a single LLM as both generator and critic. Specialisation
may produce better critique quality.

---

## Demo task (to decide)

Options under consideration:
- **Argument writing** — produce a persuasive argument; Judge scores on clarity, evidence, structure
- **Code generation** — write a function; Judge checks against a correctness rubric (or a rule expert runs tests)
- **Short essay** — improve a piece of writing; Judge applies a rubric

Preference for a task where quality improvement is visible to a reader without needing
a separate evaluation framework.

---

## Expert structure

```
missions/concepts/self-refine/
  mission.mcl
  forge.toml
  mcl.lock
  experts/
    Drafter/expert.md       ← system prompt tuned to produce and revise
    Judge/expert.md         ← rubric in body; role: judge; onFail writes {{feedback}}
  README.md
```

---

## Open questions

- What demo task makes the improvement most legible in the output?
- Should iteration count be configurable via `forge.toml` input, or fixed at 3?
- Should we show the iteration-by-iteration outputs, or just the final?
- Does the Judge's rubric live in the frontmatter or the body of `expert.md`?

---

## What success looks like

The final output is observably better than the first draft. A reader unfamiliar with MCL
can look at `mission.mcl` and immediately recognise the refinement loop. The README maps
the mission structure to the paper's Figure 1.
