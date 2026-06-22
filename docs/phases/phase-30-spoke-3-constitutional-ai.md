# Phase 30 Spoke 3 — Constitutional AI

> **Status: Brainstorm**
> **Paper:** [Constitutional AI: Harmlessness from AI Feedback (Bai et al., Anthropic 2022)](https://arxiv.org/pdf/2212.08073)

---

## Paper summary

A model critiques its own output against a set of explicit principles (the "constitution"),
then revises accordingly. Two phases in the paper: supervised revision, then RL from AI
feedback (RLAIF). The critique-revise loop produces measurable, controllable behaviour change
with far fewer human labels.

Key insight: the critique must be structured and specific — not "this is bad" but
"this violates criterion X because Y." Structured critique is what makes revision actionable.

---

## MCL expression

```
Drafter → ConstitutionalCritic → Reviser
```

- `Drafter` — produces an initial draft
- `ConstitutionalCritic` — its `expert.md` body contains explicit numbered principles;
  the critic identifies which principles are violated and why
- `Reviser` — reads the draft and the critic's structured feedback; produces a revised output

The `expert.md` body for `ConstitutionalCritic` IS the constitution — explicit, readable,
forkable. Changing the principles changes the critique without touching any code.

**What MCL adds over the paper:** the constitution is externalised into a markdown file
that any user can read and edit. No fine-tuning, no RLAIF. The Critic and Reviser are
separate role-specialised experts, not a single model running multiple passes.

---

## Demo task (to decide)

Options under consideration:
- **Writing quality** — improve a piece of prose against explicit criteria (clarity, evidence, concision)
- **Code review** — critique code against an explicit style and correctness constitution
- **Product copy** — refine marketing copy against a brand voice constitution

Preference for a task where the constitution principles are immediately legible and
where a reader can see exactly which principle triggered each revision.

---

## Expert structure

```
missions/concepts/constitutional-ai/
  mission.mcl
  forge.toml
  mcl.lock
  experts/
    Drafter/expert.md             ← produces initial output
    ConstitutionalCritic/expert.md  ← body = numbered principles; critiques against them
    Reviser/expert.md             ← reads draft + critique; produces revised output
  README.md
```

### Example constitution (in ConstitutionalCritic/expert.md body)

```markdown
You are a constitutional critic. Evaluate the draft against these principles:

1. **Clarity** — every sentence has one clear meaning. Flag ambiguous phrasing.
2. **Evidence** — every claim is supported by a reason or example. Flag unsupported assertions.
3. **Concision** — no sentence is longer than necessary. Flag padding or repetition.
4. **Tone** — professional and direct. Flag hedging or filler phrases.

For each violation, state: which principle, the offending text, and why it fails.
If no violations, output PASS.
```

---

## Open questions

- Should this extend to a `loop` (Drafter → Critic → Reviser → Critic → ...) or single-pass?
- Should the Critic output structured JSON (principle + reason + suggestion) or prose?
- Is this distinct enough from Spoke 1 (Self-Refine) to warrant a separate mission?
  Key difference: explicit named principles vs. generic quality feedback.
- What domain makes the constitution most legible to a community audience?

---

## What success looks like

The revised output demonstrably addresses the specific principles the Critic flagged.
A reader can look at `ConstitutionalCritic/expert.md`, understand the constitution,
and predict what the critic will flag. The README maps the three-expert pipeline to
the paper's critique-revise loop.
