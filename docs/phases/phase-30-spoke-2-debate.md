# Phase 30 Spoke 2 — Multi-Agent Debate

> **Status: Brainstorm**
> **Paper:** [Improving Factuality and Reasoning in Language Models through Multiagent Debate (Du et al., 2023)](https://arxiv.org/abs/2401.05998)

---

## Paper summary

Multiple LLM agents independently generate a response to the same question. Each agent
then reads all other agents' responses and revises its own. After several rounds, a
synthesis step produces the final answer.

Key results: improves arithmetic reasoning, truthfulness, and machine translation over
single-agent baselines. Models converge on a shared, higher-quality solution after debate.

Core mechanism: diverse independent perspectives → cross-agent exposure → synthesis.
Breadth first, then convergence.

---

## MCL expression

```
parallel {
    PerspectiveA
    PerspectiveB
    PerspectiveC
}
-> Synthesiser
```

- `PerspectiveA/B/C` — each expert receives the same input, generates independently,
  with no shared context during parallel execution
- `Synthesiser` — reads `{{PerspectiveA.output}}`, `{{PerspectiveB.output}}`,
  `{{PerspectiveC.output}}`; produces a synthesised answer that reasons across all three

The `parallel {}` block is the exact structural expression of the debate pattern.
Each expert is role-specialised — not just another instance of the same prompt —
which is the key extension over the paper's homogeneous agent setup.

**What MCL adds over the paper:** each parallel expert has a distinct angle baked into
its system prompt (e.g. optimist / sceptic / pragmatist, or different domain lenses).
This is role-specialised debate, not homogeneous multi-sampling.

---

## Demo task (to decide)

Options under consideration:
- **Contested reasoning question** — ethical dilemma, strategic tradeoff, policy question
- **Factual synthesis** — question with multiple valid framings (e.g. "what caused X?")
- **Technical decision** — architecture choice with real tradeoffs

Preference for a question where diverse perspectives genuinely improve the answer —
not a task with a single objectively correct answer.

---

## Expert structure

```
missions/concepts/debate/
  mission.mcl
  forge.toml
  mcl.lock
  experts/
    PerspectiveA/expert.md    ← assigned angle / lens in system prompt
    PerspectiveB/expert.md
    PerspectiveC/expert.md
    Synthesiser/expert.md     ← reads all three outputs; produces convergent answer
  README.md
```

---

## Open questions

- What angles should the three experts take? (domain lenses vs epistemic stances)
- Should this be a single-round debate (parallel → synthesise) or multi-round?
  Multi-round requires loop + feedback wiring not natively in current parallel semantics.
- Should there be a Judge that scores the synthesised answer vs a single-expert answer?
- What question makes the fan-out/fan-in structure most obviously valuable?

---

## What success looks like

The synthesised answer is richer than any single perspective alone. A reader can look
at `mission.mcl` and immediately see the fan-out/fan-in pattern. The README maps
`parallel {}` to the paper's debate rounds.
