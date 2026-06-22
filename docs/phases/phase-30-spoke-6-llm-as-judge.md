# Phase 30 Spoke 6 — LLM-as-Judge

> **Status: Brainstorm**
> **Paper:** [Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena (Zheng et al., 2023)](https://arxiv.org/abs/2306.05685)

---

## Paper summary

LLMs can reliably evaluate the quality of other LLMs' outputs — acting as judges rather
than relying solely on human evaluation. The paper introduces MT-Bench (multi-turn
reasoning benchmark) and Chatbot Arena, and shows that GPT-4 as a judge agrees with
human evaluators >80% of the time.

Key findings:
- **Pairwise comparison** (which of A or B is better) is more reliable than absolute scoring
- **Reference-guided** judging (judge sees an example of a good answer) improves consistency
- **Position bias** is a real failure mode — judges tend to favour the first response
- **Verbosity bias** — longer responses are rated higher even when not better

Practical implication: LLM-as-Judge is a scalable, credible alternative to human evaluation
for pipeline quality assessment.

---

## MCL expression

This spoke is different from the others — it is less about demonstrating a reasoning
*pipeline* and more about demonstrating MCL's `role: judge` as a first-class evaluation
instrument.

**Pattern A — Pairwise evaluation**
```
parallel {
    ExpertA
    ExpertB
}
-> PairwiseJudge
```
`PairwiseJudge` reads both outputs and decides which is better, with reasoning.

**Pattern B — Reference-guided scoring**
```
ExpertUnderTest → ReferenceJudge
```
`ReferenceJudge/expert.md` contains a reference answer or rubric in its body; scores
the expert's output against it.

**Pattern C — Pipeline quality gate (the native MCL use)**
```
Drafter → Judge → (pass: done | fail: loop back)
```
This is the existing Judge pattern already used in loop missions. The spoke makes it
explicit as an LLM-as-Judge demonstration.

MCL's `role: judge` is the direct implementation of what the paper proposes.
The expert.md body is the evaluation rubric. `onFail` behaviour is the quality gate.

**What MCL adds over the paper:** the Judge is composable — it is just another expert
in the pipeline. You can chain judges (a Critic judges the Drafter, a MetaJudge judges
the Critic). The paper treats the judge as an external evaluator; MCL makes it an
internal pipeline participant.

---

## Demo task (to decide)

Options under consideration:
- **Pairwise comparison** — run two different Drafter system prompts in parallel; Judge
  picks the better answer with reasoning (demonstrates position-bias awareness)
- **Rubric scoring** — a single Drafter produces an answer; ReferenceJudge scores it
  against explicit criteria and an example reference answer
- **Meta-judge** — a Judge evaluates a Drafter; a MetaJudge evaluates the Judge's critique
  (demonstrates composability that the paper does not address)

Preference for a demo that shows something the paper's external evaluation setup cannot —
the Judge as a pipeline participant that influences downstream behaviour.

---

## Expert structure

```
missions/concepts/llm-as-judge/
  mission.mcl
  forge.toml
  mcl.lock
  experts/
    Drafter/expert.md           ← produces output under evaluation
    ReferenceJudge/expert.md    ← role: judge; rubric + reference answer in body
  README.md
```

Or for pairwise variant:
```
  experts/
    DrafterA/expert.md
    DrafterB/expert.md          ← same task, different system prompt / approach
    PairwiseJudge/expert.md     ← role: judge; compares A vs B; explains preference
```

---

## Open questions

- Which pattern (pairwise, reference-guided, or meta-judge) is most compelling as a demo?
- Should the Judge output a numeric score, a qualitative verdict, or both?
- Should we explicitly demonstrate position-bias mitigation (run judge twice with A/B order
  swapped and compare)?
- Is the meta-judge pattern (judge the judge) worth its own variant, or too complex for a
  concept mission?
- Does this spoke overlap too much with the Judge pattern already used in Spoke 1
  (Self-Refine)? The distinction is: Spoke 1 uses Judge as a gate; this spoke makes
  Judge the *subject* of the demo.

---

## What success looks like

A reader understands that MCL's `role: judge` is a principled implementation of what
the Zheng et al. paper proposes — not an ad hoc quality check. The README maps
`role: judge` in `expert.md` frontmatter to the paper's judge architecture.
The demo shows the Judge's output (score + reasoning) alongside the Drafter's output,
making the evaluation transparent.
