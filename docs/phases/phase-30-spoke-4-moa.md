# Phase 30 Spoke 4 — Mixture of Agents

> **Status: Brainstorm**
> **Paper:** [Mixture-of-Agents Enhances Large Language Model Capabilities (Wang et al., 2024)](https://arxiv.org/abs/2406.04692)

---

## Paper summary

Multiple LLM agents in multiple rounds. In each round, each agent generates a response
having seen all other agents' responses from the prior round. A final aggregator layer
synthesises the best answer.

Key result: a MoA of medium-sized models surpasses GPT-4 Omni on AlpacaEval 2.0,
FLASK, and MT-Bench. The gain comes from two properties:
1. **Collaborativeness** — LLMs produce better output when given other models' responses
   as reference, even if those references are wrong or weaker.
2. **Role specialisation** — different agents contribute different strengths to the pool.

The paper tested homogeneous agents (same model, same prompt). Role-specialised agents
are an open extension.

---

## MCL expression

```
Analyst → Strategist → Implementer → Reviewer
```

Each expert in a sequential pipeline sees the full accumulated context — all prior
experts' outputs are available. This is the MoA accumulation pattern: each stage
enriches the context that the next stage reads.

**What MCL adds over the paper:** each expert is role-specialised with a distinct system
prompt. The paper's agents had identical prompts and were distinguished only by model
sampling. MCL's version is role-differentiated MoA — potentially stronger because
each expert is optimised for its specific stage.

This is also the concept that the research.md notes as "the strongest validation of
MCL's thesis." The sequential pipeline is not a workaround — it is MoA with better
role definition.

---

## Demo task (to decide)

Options under consideration:
- **Technical problem decomposition** — a system design problem decomposed across Analyst
  (understand the problem), Strategist (propose approach), Implementer (detail the solution),
  Reviewer (critique and improve)
- **Research synthesis** — Analyst (identify key questions), ResearcherA/B (investigate
  sub-questions), Synthesiser (combine findings)
- **Decision making** — FrameProblem → GenerateOptions → EvaluateOptions → Recommend

Preference for a task where each stage visibly adds something the prior stage could not
— demonstrating that sequential accumulation produces compound improvement.

---

## Expert structure

```
missions/concepts/mixture-of-agents/
  mission.mcl
  forge.toml
  mcl.lock
  experts/
    Analyst/expert.md       ← frames and understands the problem
    Strategist/expert.md    ← proposes approach given the analysis
    Implementer/expert.md   ← details the solution given strategy
    Reviewer/expert.md      ← critiques and strengthens the full output
  README.md
```

---

## Open questions

- What 4-expert task makes the stage-by-stage accumulation most observable?
- Should the Reviewer be `role: judge` to give it a formal pass/fail gate?
- Should we include a single-expert baseline (same question, no pipeline) for comparison?
- Is 4 experts the right depth, or is 3 more legible?
- How do we show the "collaborativeness" property — that each stage benefits from
  reading prior context — without a formal ablation?

---

## What success looks like

The final output is richer than any single expert could produce alone. A reader can
trace how each expert's contribution builds on the prior. The README maps the sequential
pipeline to the paper's layered agent rounds and explains why role specialisation extends
the paper's findings.
