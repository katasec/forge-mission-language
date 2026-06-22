# Multi-Agent Debate

## 1. Foundational concept

Multi-Agent Debate is a technique in which multiple LLM agents independently generate
a response to the same question, then each agent reads all other agents' responses and
may revise — with the final answer emerging through synthesis.

As Du et al. (2023) describe: *"We find that this debate process leads to significant
improvements in mathematical reasoning, strategic question-answering, and the generation
of more factual content across various tasks."* The key mechanism is that diverse
independent perspectives surface reasoning paths that no single agent would find alone.

MCL makes this pattern first-class: the `parallel {}` block IS the independent
generation phase. The Synthesiser IS the convergence phase. The architecture of the
paper and the structure of the mission file are the same artifact.

## 2. References

**Papers:**
- Du, Y., et al. — "Improving Factuality and Reasoning in Language Models through
  Multiagent Debate" — 2023 — https://arxiv.org/abs/2401.05998
  Key results: improved arithmetic reasoning (GSM8K), truthfulness (TruthfulQA), and
  machine translation quality over single-agent baselines. Models converge on higher-
  quality solutions after debate rounds.
- Dziri, N., et al. — "Faith and Fate: Limits of Transformers on Compositionality" —
  NeurIPS 2024 — https://arxiv.org/abs/2307.05471
  Referenced by the Sceptic expert: systematic evidence that LLM accuracy degrades
  with compositional depth — the empirical ground for the sceptical position.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  Contextualises debate as part of the broader shift from monolithic models to compound
  systems where "state-of-the-art AI results are increasingly obtained by systems
  with multiple components."

## 3. How MCL demonstrates this

```
// Multi-Agent Debate — diverse perspectives converging on a better answer
// Du et al., 2023 (https://arxiv.org/abs/2401.05998)

let question = "Can large language models truly reason, or are they sophisticated
pattern matchers that simulate reasoning without understanding?"

mission Debate(question) = {
    parallel {
        Optimist
        Sceptic
        Pragmatist
    }
    -> Synthesiser
}

output(Debate)
```

The `parallel {}` block is the independent generation phase — three concurrent LLM
calls, each with a distinct epistemic stance baked into its system prompt. `Synthesiser`
is the convergence phase — it reads `{{Optimist.output}}`, `{{Sceptic.output}}`, and
`{{Pragmatist.output}}` and produces a single answer that only becomes possible after
seeing all three views.

### Example A — the paper's own problem domain

Du et al. (2023) validated debate on *reasoning and factuality tasks* — exactly the
domain in dispute in this mission's question. The question — "Can large language models
truly reason?" — is itself a reasoning and factuality question about LLMs, directly
within the paper's validation domain.

```
parallel {
    Optimist     ← argues LLMs show genuine reasoning
    Sceptic      ← argues they are sophisticated pattern matchers
    Pragmatist   ← argues the question is the wrong frame; focus on architecture
}
-> Synthesiser
```

Each expert receives the same question and generates independently (concurrent LLM
calls). The Synthesiser reads all three outputs and produces a convergent answer that
incorporates insights from all three positions. Du et al. report that this kind of
cross-agent exposure consistently improves factual accuracy and reasoning quality over
any single agent.

**What MCL adds over the paper:** Du et al. used homogeneous agents (identical prompts,
different sampling). MCL uses role-specialised agents — each expert has a distinct
epistemic stance baked into its system prompt. Role-specialised debate may produce more
diverse and complementary perspectives than homogeneous sampling.

### Example B — general variant

Replace the `question` variable with any contested reasoning problem:
- A technical architecture decision with real tradeoffs
- A strategic business question with legitimate competing views
- A policy question where domain expertise from multiple angles matters

The three experts' stances (Optimist, Sceptic, Pragmatist) apply to almost any complex
question. The Synthesiser's structure (reveals / converges / resolves / implies) is a
reusable synthesis template.

## 4. Why this is normally hard

Without MCL, implementing multi-agent debate requires:
- Three concurrent LLM API calls (async/await, error handling for each)
- Collecting and formatting all three responses into a prompt for the Synthesiser
- A fourth LLM API call for the Synthesiser with the concatenated context
- Orchestration code to manage concurrency, timeouts, and partial failures
- State management to pass the right outputs to the right next step

In LangChain, concurrent agent calls require understanding of `RunnableParallel`,
output parsers, and prompt chaining. A developer who understands these tools can build
it in a day. A domain expert who wants to add a fourth debate perspective — or change
the Sceptic's framing — needs a developer to help them.

**With MCL:**

```
parallel {
    Optimist
    Sceptic
    Pragmatist
}
-> Synthesiser
```

Adding a fourth expert is one line. Changing an expert's epistemic stance is editing
a markdown file. The concurrency and output routing are handled by the runtime.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission Debate
```

To debate a different question, edit the `let question` line in `mission.mcl`.
