# LLM-as-Judge

## 1. Foundational concept

LLM-as-Judge is a scalable evaluation technique in which a language model acts as a
quality evaluator for other language models' outputs — replacing or supplementing
expensive human evaluation.

Zheng et al. (2023) introduced this in the MT-Bench paper, demonstrating that GPT-4 as
a judge *"achieves over 80% agreement with human experts"* across multi-turn reasoning
tasks. They studied three judging formats: pairwise comparison, single-answer scoring,
and reference-guided scoring. Reference-guided judging — where the judge sees an example
of a correct answer alongside an explicit rubric — is the most reliable and consistent
of the three.

MCL's `role: judge` is the direct implementation of what the paper proposes. The
`expert.md` body is the evaluation rubric. The reference answer lives in the same file
as the rubric. The judge is a pipeline participant — not an external evaluator called
after the fact.

## 2. References

**Papers:**
- Zheng, L., et al. — "Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena" —
  2023 — https://arxiv.org/abs/2306.05685
  Key results: GPT-4 as judge agrees with human evaluators >80% of the time on
  multi-turn reasoning tasks. Reference-guided judging outperforms pairwise comparison
  in consistency. Position bias and verbosity bias are documented failure modes.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  LLM-as-Judge is cited as a core component of compound AI evaluation infrastructure.

## 3. How MCL demonstrates this

```
// LLM-as-Judge — reference-guided quality evaluation
// Zheng et al., 2023 (https://arxiv.org/abs/2306.05685)

let question = "Explain the concept of gradient descent to a software engineer
who understands calculus but has never studied machine learning. Include why
it works, what can go wrong, and one concrete example."

mission LLMAsJudge(question) = {
    Drafter
    -> ReferenceJudge
}

output(LLMAsJudge)
```

Two experts. The `Drafter` answers the question. The `ReferenceJudge` carries
`role: judge` and contains — inside its own `expert.md` — a 5-criterion rubric
(2 pts each, total 10), a reference answer for calibration, and a pass threshold
of 7/10. The rubric and reference answer are readable by anyone; changing the
evaluation criteria means editing a markdown file.

### Example A — the paper's own task format (MT-Bench style)

Zheng et al. designed MT-Bench around multi-turn technical explanation tasks — exactly
the kind of question this mission evaluates. The question (explain gradient descent to
a calculus-familiar engineer) is a prototypical MT-Bench task: it requires accurate
technical content, audience calibration, and structured completeness.

```
Drafter → ReferenceJudge
```

The `ReferenceJudge` implements Zheng et al.'s reference-guided scoring format:
- An explicit 5-criterion rubric (2 pts each, total 10)
- A reference answer for calibration (what a strong answer looks like)
- A clear pass threshold (≥7/10)
- Criterion-level breakdown in the output

This produces a structured, reproducible evaluation that maps directly to the paper's
methodology. The judge's output includes a score, per-criterion reasoning, and a verdict.

**What MCL adds over the paper:** the Judge is a composable pipeline participant.
It is not called externally after the fact — it runs inside the same pipeline as the
Drafter. This means a failing score can trigger a loop (add `loop(N)` to the mission)
or route to a Reviser. The paper treats the judge as an evaluator; MCL makes it an
actor that can influence the pipeline's behaviour.

### Example B — general professional output

Replace the `question` variable with any task where quality is evaluable against a
rubric:
- A written customer escalation response (rubric: empathy, resolution clarity, tone)
- A code review comment (rubric: specificity, actionability, accuracy, constructiveness)
- A project status update (rubric: completeness, clarity, risk visibility, decision
  requests)

Edit `experts/ReferenceJudge/expert.md` to change the rubric and reference answer.
The scoring format remains the same.

## 4. Why this is normally hard

Without MCL, implementing reference-guided LLM evaluation requires:
- A Drafter LLM call
- A Judge LLM call that receives: the question, the draft, the rubric, and the reference
  answer — carefully formatted into a single prompt
- Parsing the Judge's structured output (score, per-criterion breakdown, pass/fail)
- Storing the rubric and reference answer somewhere accessible (code constant, config,
  database)
- Wiring the Judge's verdict to any downstream action (retry, escalate, log)

The rubric lives in code or config — meaning a domain expert who wants to add a sixth
evaluation criterion needs a developer to change a string constant and redeploy.

Zheng et al. also document two failure modes: position bias (the judge favours the first
response when comparing A vs B) and verbosity bias (longer responses score higher
regardless of quality). Reference-guided scoring mitigates verbosity bias by anchoring
scoring to explicit criteria rather than overall length.

**With MCL:**

The rubric and reference answer are in `experts/ReferenceJudge/expert.md`. A domain
expert can read the rubric, add a criterion, change the pass threshold, or update the
reference answer — all without touching code. The judge is a pipeline participant that
can gate downstream steps.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission LLMAsJudge
```

To evaluate a different type of output, edit `let question` in `mission.mcl` and update
the rubric and reference answer in `experts/ReferenceJudge/expert.md`.
