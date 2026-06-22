# Self-Refine

## 1. Foundational concept

Self-Refine is an iterative improvement technique in which a language model generates an
initial output, critiques it against an explicit rubric, then refines the output using
that critique — without any fine-tuning.

The key requirement is that the critique be specific and actionable. As Madaan et al.
(NeurIPS 2023) state: *"LLMs can be further improved at test-time using this simple,
standalone approach — without any additional training."* Generic feedback ("make it
better") produces little gain; criterion-level feedback ("the second reason has no named
source") produces the ~20% improvement the paper reports.

## 2. References

**Papers:**
- Madaan, A., et al. — "Self-Refine: Iterative Refinement with Self-Feedback" —
  NeurIPS 2023 — https://arxiv.org/abs/2303.17651
  Key result: ~20% absolute improvement across 7 diverse tasks (dialog, code, math
  reasoning) using GPT-3.5, ChatGPT, and GPT-4.
- Shinn, N., et al. — "Reflexion: Language Agents with Verbal Reinforcement Learning" —
  NeurIPS 2023 — https://arxiv.org/abs/2303.11366
  Extends Self-Refine with persistent episodic memory buffers across episodes.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  Documents the broader trend: iterative refinement loops as a core pattern in
  state-of-the-art AI systems.

## 3. How MCL demonstrates this

```
// Self-Refine — iterative improvement via targeted critique
// Madaan et al., NeurIPS 2023 (https://arxiv.org/abs/2303.17651)

let topic = "whether AI research results should be published openly or kept proprietary"

mission SelfRefine(topic) loop(3) = {
    Drafter
    -> QualityJudge
}

output(SelfRefine)
```

The entire pattern is three lines. `loop(3)` means the pipeline repeats up to three
times. `QualityJudge` carries `role: judge` — when it fails it writes the specific
critique to `{{feedback}}`, which the `Drafter` sees on the next attempt. The loop
exits early the moment the Judge passes.

### Example A — the paper's own task domain

Madaan et al. validated Self-Refine across 7 tasks including *argument writing* and
*code optimisation*, reporting ~20% absolute improvement over single-pass generation.
The argument writing task is their clearest demonstration: a single-shot argument is
observably weaker than a rubric-critiqued revision.

This mission replicates that exact experiment: the same model writes a persuasive
argument, then critiques it against an explicit rubric, then revises. The loop exits
when all three criteria pass or after 3 attempts.

The `QualityJudge` carries `role: judge`. Its body IS the rubric. When it fails, the
failure reason becomes `{{feedback}}`, which the `Drafter` sees verbatim on the next
attempt. This is Self-Refine's critique signal made explicit and inspectable.

**What MCL adds over the paper:** Drafter and Judge are separate, role-optimised
experts. Self-Refine uses a single LLM as both generator and critic. Separation allows
each expert to be tuned for its specific role.

### Example B — general variant

Replace the `topic` variable with any domain: a compliance policy draft, a grant
proposal executive summary, a technical architecture decision. The `QualityJudge` rubric
in `experts/QualityJudge/expert.md` can be edited to match the domain's criteria.

A compliance officer can write the rubric in plain English. No code changes required.

## 4. Why this is normally hard

Without MCL, implementing Self-Refine requires:
- An LLM API client for the Drafter call
- A separate LLM call for the Judge, passing the Drafter's output as input
- Manual retry logic: parse the Judge's JSON, detect "fail", format a new prompt with
  the failure reason, re-call the Drafter API
- State management: tracking iteration count, storing feedback across attempts
- Error handling for API failures vs. evaluation failures

A typical Python implementation spans 60–100 lines across multiple files and requires
understanding of prompt templates, structured output parsing, and retry/backoff logic.
Even with LangChain, the loop and feedback injection require non-trivial chain composition.

**With MCL** (the full file is shown at the top of this section):

The rubric lives in `experts/QualityJudge/expert.md`. A domain expert can read it,
modify it, and run the mission without writing a line of code. The loop, feedback
injection, and early exit are provided by the runtime.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission SelfRefine
```

To test with a different topic, edit the `let topic` line in `mission.mcl`.
