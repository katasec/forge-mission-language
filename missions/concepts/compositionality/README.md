# Compositionality — Novel Tasks from Known Primitives

## 1. Foundational concept

Compositionality is the ability to solve novel, unfamiliar problems by combining known
building blocks in new ways. It is a fundamental property of human intelligence — we
handle novel situations by reassembling what we already know.

LLMs struggle with this. Dziri et al. (NeurIPS 2024) show systematically that *"as
the level of composition increases, the accuracy degrades"* — even when the model
handles each component correctly in isolation. The model pattern-matches against
training data; it cannot reliably *compose* rules it has not seen combined before.

The neurosymbolic solution: decompose complex novel tasks into known sub-tasks, assign
each sub-task to a specialised expert, then compose the results. As Marcus argues:
*"Embeddings aren't enough. Sooner or later you are going to need true compositionality."*
And BAIR (Zaharia et al., 2024): *"State-of-the-art AI results are increasingly obtained
by compound systems with multiple components, not just monolithic models."*

MCL makes this explicit: the `TaskDecomposer` makes the routing transparent. The
`parallel {}` block shows exactly which primitives run. The `Composer` shows how they
are assembled. Anyone reading the mission file can see the composition.

## 2. References

**Papers:**
- Dziri, N., et al. — "Faith and Fate: Limits of Transformers on Compositionality" —
  NeurIPS 2024 — https://arxiv.org/abs/2307.05471
  Systematic study showing transformers fail on compositional tasks even when they
  master each component; accuracy degrades as composition depth increases.
- Feng, L., et al. — "Composition of Experts / CoE" — 2024 —
  https://arxiv.org/pdf/2412.01868
  Modular specialised models composed via routing outperform monolithic models.
- Ma, S., et al. — "Symbolic MoE: Adaptive Skill-based Routing for Heterogeneous
  Reasoning" — 2025 — https://arxiv.org/pdf/2503.05641
  Symbolic routing to heterogeneous specialised experts for composable reasoning.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  *"Compound systems with multiple specialised components achieve SOTA, not monolithic
  models."*
- IBM — "What Are Compound AI Systems?" —
  https://www.ibm.com/think/topics/compound-ai-systems
- Gary Marcus — on compositionality as the core missing property of LLMs

## 3. How MCL demonstrates this

```
// Compositionality — Novel Tasks from Known Primitives
// "Faith and Fate" (Dziri et al., NeurIPS 2024) + CoE (2024) + BAIR (2024)

let task = "Evaluate this investment memo excerpt: 'Our target market of 50M
small businesses will pay $99/month for our AI bookkeeping tool. Based on a 5%
conversion rate from our pilot data, we project $247M ARR in year one. Our
competitive moat is our proprietary dataset and the enthusiastic tone our early
customers have shown in feedback surveys.'"

mission Compositionality(task) = {
    TaskDecomposer
    -> parallel {
        FactAnalyst
        NumberChecker
        ToneReviewer
    }
    -> Composer
}

output(Compositionality)
```

`TaskDecomposer` explicitly names what each primitive should look for in this
specific task. The three parallel experts run independently — each focused on one
well-defined analytical primitive that they handle reliably in isolation. `Composer`
reads `{{FactAnalyst.output}}`, `{{NumberChecker.output}}`, and
`{{ToneReviewer.output}}` and assembles a verdict only possible by combining all
three. The composition is declared, not coded.

### Example A — the paper's compositional failure mode

Dziri et al. specifically identify *multi-step analytical tasks* as where transformers
fail most severely — tasks requiring a model to simultaneously check facts, verify
numbers, and evaluate language. A single-model approach to this task tends to mix
levels of analysis, miss specific issues, and produce holistic judgements that are hard
to verify.

This mission decomposes exactly such a task:

```
TaskDecomposer
-> parallel {
    FactAnalyst    ← evaluates factual claims and logical consistency
    NumberChecker  ← checks arithmetic and quantitative reasoning
    ToneReviewer   ← identifies where tone substitutes for evidence
}
-> Composer
```

- **TaskDecomposer** makes the decomposition explicit: it names what each primitive
  should look for in this specific task
- **Three primitive experts** run independently (concurrent LLM calls), each focusing
  on one well-defined analytical primitive
- **Composer** reads all three outputs and assembles a verdict that is only possible
  by combining all three perspectives

The composition is transparent: anyone reading the mission file can see exactly how
the novel task is split and how the pieces are reassembled.

**What MCL adds over the paper:** Dziri et al. document the failure. CoE documents the
fix (modular experts). MCL makes the fix readable, forkable, and runnable without
writing orchestration code.

### Example B — general compositional task

The three primitives (FactAnalyst, NumberChecker, ToneReviewer) apply to almost any
complex evaluative task:
- Grant proposal evaluation (factual claims, budget arithmetic, narrative quality)
- Legal contract review (factual assertions, clause arithmetic, language clarity)
- Technical documentation audit (accuracy, measurement consistency, readability)

Replace the `task` variable. The `TaskDecomposer` generates instructions for each
primitive based on the new task automatically.

## 4. Why this is normally hard

Without MCL, compositional task routing requires:
- An LLM call to decompose the task (and code to parse its output into routing decisions)
- Dynamic dispatch to the right expert based on the decomposition
- Parallel execution orchestration (async, thread pools, or agent frameworks)
- Result aggregation: collecting outputs from parallel experts and passing them to the
  composer
- Error handling if any sub-task fails

In practice, most teams implement this with a monolithic prompt ("please check the
facts, the numbers, and the tone") and hope the model handles the composition. The
research shows it often does not — the model either misses issues in one dimension or
produces a holistic judgement that cannot be traced to specific findings.

The alternative — building proper compositional routing in LangChain, AutoGen, or a
custom agent framework — requires significant engineering. The analyst who knows which
primitives to apply cannot build the system today.

**With MCL:**

```
TaskDecomposer
-> parallel { FactAnalyst, NumberChecker, ToneReviewer }
-> Composer
```

The composition is declared, not coded. Adding a fourth primitive (e.g. a
`RegulatoryChecker` for legal domains) is one line in the mission file and a new
`expert.md`. The domain expert IS the architect.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission Compositionality
```

To evaluate a different type of document or task, edit the `let task` line in
`mission.mcl`. The `TaskDecomposer` will generate appropriate instructions for each
primitive expert automatically.
