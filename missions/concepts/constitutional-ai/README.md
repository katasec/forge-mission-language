# Constitutional AI

## 1. Foundational concept

Constitutional AI (CAI) is a technique in which a language model critiques its own
output against a set of explicit, named principles — the "constitution" — and then
revises accordingly. The constitution replaces ad hoc human feedback with a structured,
repeatable evaluation process.

Bai et al. (Anthropic, 2022) describe the core mechanism: *"We use a set of principles
to guide the AI to be helpful, harmless, and honest, and have the AI critique its own
outputs and revise them according to those principles."* The key insight is that
critique must be structured — not "this is bad" but "this violates principle 3 because
the phrase 'complex process' is undefined." Structured critique is what makes revision
actionable.

In MCL, the `ConstitutionalCritic/expert.md` file IS the constitution. The principles
are numbered markdown items in the body of the expert. Changing the principles means
editing a markdown file — no code changes, no fine-tuning, no API access required.

## 2. References

**Papers:**
- Bai, Y., et al. — "Constitutional AI: Harmlessness from AI Feedback" — Anthropic,
  2022 — https://arxiv.org/pdf/2212.08073
  Key result: more harmless outputs with far fewer human labels; the critique-revise
  loop produces measurable, controllable behaviour change.
- Madaan, A., et al. — "Self-Refine: Iterative Refinement with Self-Feedback" —
  NeurIPS 2023 — https://arxiv.org/abs/2303.17651
  Related work: Self-Refine uses generic quality feedback; CAI uses named, structured
  principles. The distinction produces different critique quality.

**Industry/blog:**
- Anthropic — "Constitutional AI" — Research page —
  https://www.anthropic.com/research/constitutional-ai-harmlessness-from-ai-feedback
- Anthropic — "Claude's Constitution" —
  https://www.anthropic.com/constitution
  The real-world constitution used to train Claude. The `ConstitutionalCritic/expert.md`
  in this mission is a simplified, domain-specific analogue.

## 3. How MCL demonstrates this

```
// Constitutional AI — structured critique against explicit principles
// Bai et al., Anthropic 2022 (https://arxiv.org/pdf/2212.08073)

let task = "Explain how neural networks learn, for an executive audience
with no machine learning background"

mission ConstitutionalAI(task) = {
    Drafter
    -> ConstitutionalCritic
    -> Reviser
}

output(ConstitutionalAI)
```

Three experts, three roles. The `Drafter` produces a first draft. The
`ConstitutionalCritic` evaluates it against four numbered principles stored
in its own `expert.md` — that file IS the constitution. The `Reviser` reads
both the draft and the critique and produces the final version. Changing the
constitution means editing a markdown file; no code changes.

### Example A — the paper's critique-revise mechanism

Bai et al.'s paper centres on the critique-revise loop as the core mechanism for
producing better, more aligned outputs without human labelling. This mission implements
that exact mechanism for a professional writing task:

```
Drafter → ConstitutionalCritic → Reviser
```

The `ConstitutionalCritic` evaluates the draft against four named principles: Clarity,
Jargon-free, Concision, Accuracy. For each violation, it outputs the exact offending
phrase, why it fails, and a suggested fix — structured critique that the Reviser can
act on directly.

The Reviser then produces a final version that addresses every flagged violation without
changing anything that passed. The revision is traceable: every change maps to a named
principle.

**What MCL adds over the paper:** the constitution is externalised into a markdown file
that any user can read and edit. No fine-tuning. No RLAIF. The Critic and Reviser are
separate role-specialised experts — not a single model running multiple passes. The
roles can be optimised or replaced independently.

### Example B — custom constitution for any domain

Fork `experts/ConstitutionalCritic/expert.md` and replace the four principles with
domain-specific ones:
- Legal writing: Precision, Citation, Plain-language accessibility, Risk disclosure
- Engineering documentation: Reproducibility, Scope clarity, Assumption explicitness,
  Format compliance
- Medical communication: Accuracy, Lay accessibility, Uncertainty disclosure,
  Action clarity

The `task` variable in `mission.mcl` sets what gets drafted. The constitution sets
what quality means. Together they define a fully customisable, automated quality
pipeline — readable by any domain expert.

## 4. Why this is normally hard

Without MCL, implementing the Constitutional AI critique-revise loop requires:
- A Drafter LLM call
- A Critic LLM call that receives the draft and evaluates it against principles stored
  somewhere (a string constant in code, a config file, a database)
- Parsing the Critic's output to determine which principles failed
- A Reviser LLM call that receives both the draft and the critique
- Routing logic to handle PASS vs. violation responses

The principles live in code — meaning changing the constitution requires a developer
to edit a string constant, redeploy, and re-test. A domain expert who knows their
field's quality criteria cannot change the constitution themselves.

**With MCL:**

The constitution is `experts/ConstitutionalCritic/expert.md`. A compliance officer,
a medical writer, or an engineering manager can open this file, add a principle, and
run the mission. No code changes. No developer required.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission ConstitutionalAI
```

To try your own constitution, edit the numbered principles in
`experts/ConstitutionalCritic/expert.md`. To change the writing task, edit the
`let task` line in `mission.mcl`.
