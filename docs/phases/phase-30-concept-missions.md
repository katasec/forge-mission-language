# Phase 30 — Concept Missions (Research Paper Demonstrations)

> **Status: Brainstorm / In refinement**
> **Depends on:** Phase 22b (ONNX) for Spoke 5 (hybrid LLM + ML). Spokes 1–4 and 6–9 are LLM/rule-only and can proceed immediately.
> **Purpose:** Prove that MCL can express foundational LLM and neurosymbolic reasoning
> patterns from academic literature in a form that is readable, runnable, and accessible
> to a technical and non-technical community audience alike.

---

## Core thesis

The academic literature on LLM reasoning patterns — Self-Refine, Multi-Agent Debate,
Constitutional AI, Mixture of Agents, neurosymbolic hybrids — describes architectures
that are typically implemented buried in Python orchestration code, LangChain pipelines,
or custom agent frameworks. These are inaccessible to domain experts (finance, medicine,
law, operations) who understand the *concept* but cannot build the system.

MCL makes these patterns first-class language constructs. The `mission.mcl` file **is**
the architecture — readable by a non-technical domain expert, forkable, reproducible,
and citable back to the paper that validated the approach.

**MCL as neurosymbolic orchestration language:** MCL's kind system maps directly to the
neural/symbolic stack the research community now agrees is necessary:

| Research layer | MCL primitive |
|---|---|
| Neural generation / fluency | `kind: llm` |
| Symbolic rules / deterministic checks | `kind: rule` |
| Embedded classical ML models | `kind: onnx` |
| Structured data bridge (neural → symbolic) | `kind: json_extract` |
| Verification / feedback loop | `loop` + `role: judge` + `{{feedback}}` |
| Symbolic scaffolding / harness | the `mission.mcl` file itself |

This framing is validated by Gary Marcus ("Claude Code is an immense neurosymbolic
effort"), the BAIR compound AI systems paper (Zaharia et al., 2024), and the emerging
consensus from Anthropic, DeepMind, and OpenAI that hybrid systems outperform monolithic
LLMs.

---

## The accessibility problem (why this matters)

A finance professional understands "get three independent analysts to assess a trade,
then have a senior analyst synthesise their views" — that is exactly how trading desks
work. What they cannot do is implement Multi-Agent Debate in Python.

MCL lets them write:

```
parallel { MarketAnalyst, RiskAnalyst, SectorAnalyst }
-> SeniorSynthesiser
```

That IS the system. No Python, no API orchestration, no async code, no LangChain.
The concept and the implementation are the same artifact.

This accessibility angle is the unifying thread across all concept missions. Each
mission demonstrates that a foundational reasoning pattern — validated by peer-reviewed
research — is expressible in a form a domain expert can read, understand, and modify.

---

## Standard format for all concept mission docs

Every spoke follows this structure. Section 3 always has two examples in order.

```
## 1. Foundational concept
Plain-language explanation. Readable by a domain expert with no ML background.
No jargon without immediate definition.
Include a direct quote from the paper or a key author where possible —
their words, not a paraphrase. Clearly attributed with author, venue, year.

## 2. References
- At least one peer-reviewed paper (arxiv or published venue) — full citation:
  Author(s), Title, Venue, Year, URL
- At least one reputable blog/industry source (BAIR, Anthropic, Databricks,
  IBM, OpenAI, DeepMind) — author, title, publication, date, URL
- Where a key result or claim is made in section 1 or 3, cite inline:
  (Author et al., Year) — same style as academic writing

## 3. How MCL demonstrates this

### Example A — the paper's own problem
Start with the exact task or domain the authors used to validate the idea.
Quote the paper's own description of the task where possible.
Ground the MCL in the paper's own evidence. Cite the specific result
(e.g. "improved accuracy by X% over single-agent baseline").
.mcl sketch + expert descriptions for this task.

### Example B — a domain-specific variant
A real-world professional domain (finance, legal, operations, etc.) where
the same pattern applies. Shows the concept generalises beyond the paper's
test case. The finance trading desk, the compliance officer, the operations
manager — a person who understands the problem but cannot currently build
the system.
.mcl sketch + expert descriptions for this task.

## 4. Why this is normally hard
What tooling/expertise would normally be required. What the Python/framework
equivalent would look like and why it's opaque. The punchline: MCL reduces
this to a readable file the domain expert from Example B can understand,
fork, and run.
```

**The intent:** collaborative expression, not a sales pitch. Citations are
first-class — every claim traces back to a named author, paper, or source.
We start with the researcher's own problem and prove their finding in MCL.
The domain example shows accessibility. Together they say: "this is real,
proven, and now anyone can use it."

---

## Hub + Spokes

| Spoke | Concept | Paper | Industry/Blog source | Status |
|-------|---------|-------|----------------------|--------|
| [Spoke 1](phase-30-spoke-1-self-refine.md) | Self-Refine | Madaan et al., NeurIPS 2023 | — | Brainstorm |
| [Spoke 2](phase-30-spoke-2-debate.md) | Multi-Agent Debate | Du et al., 2023 | BAIR compound AI, 2024 | Brainstorm |
| [Spoke 3](phase-30-spoke-3-constitutional-ai.md) | Constitutional AI | Bai et al., Anthropic 2022 | Anthropic research blog | Brainstorm |
| [Spoke 4](phase-30-spoke-4-moa.md) | Mixture of Agents | Wang et al., 2024 | Databricks / IBM compound AI | Brainstorm |
| [Spoke 5](phase-30-spoke-5-hybrid.md) | LLM + Classical ML | CoE / compound AI literature | Marcus & Belle, AAAI 2025 | Brainstorm (needs Phase 22b) |
| [Spoke 6](phase-30-spoke-6-llm-as-judge.md) | LLM-as-Judge | Zheng et al., 2023 | — | Brainstorm |
| [Spoke 7](phase-30-spoke-7-hallucination-reduction.md) | Hallucination Reduction via Symbolic Grounding | Marcus neurosymbolic thesis; RLSF 2024 | Gary Marcus (Substack); IBM neurosymbolic | Brainstorm |
| [Spoke 8](phase-30-spoke-8-verifiable-reasoning.md) | Verifiable Step-by-Step Reasoning | AlphaGeometry (DeepMind, 2024/2025); AlphaProof | DeepMind blog; Marcus on hybrid provers | Brainstorm |
| [Spoke 9](phase-30-spoke-9-compositionality.md) | Compositionality — Novel Tasks from Primitives | "Faith and Fate" (2024); CoE (2024) | BAIR compound AI; Marcus on embeddings | Brainstorm |
| [Spoke 10](phase-30-spoke-10-program-synthesis.md) | Program Synthesis — Dynamic Mission Generation | AlphaCode (DeepMind, 2022); PAL (Gao et al., 2022); Self-Debugging (Chen et al., 2023) | Marcus & Belle, AAAI 2025 | Design |

---

## Key sources (consolidated)

**Academic papers:**
- [Self-Refine (Madaan et al., NeurIPS 2023)](https://arxiv.org/abs/2303.17651)
- [Reflexion (Shinn et al., NeurIPS 2023)](https://arxiv.org/abs/2303.11366)
- [Multi-Agent Debate (Du et al., 2023)](https://arxiv.org/abs/2401.05998)
- [Constitutional AI (Bai et al., Anthropic 2022)](https://arxiv.org/pdf/2212.08073)
- [Mixture of Agents / MoA (Wang et al., 2024)](https://arxiv.org/abs/2406.04692)
- [Composition of Experts / CoE (2024)](https://arxiv.org/pdf/2412.01868)
- [LLM-as-Judge (Zheng et al., 2023)](https://arxiv.org/abs/2306.05685)
- [Symbolic MoE: Adaptive Skill-based Routing (2025)](https://arxiv.org/pdf/2503.05641)
- [RLSF: Fine-tuning LLMs via Symbolic Feedback (2024)](https://arxiv.org/pdf/2405.16661)
- [Faith and Fate: Limits of Transformers on Compositionality (2024)](https://arxiv.org/abs/2307.05471)
- [Neurosymbolic AI: Comparative Study of Logical Reasoning (2025)](https://www.arxiv.org/pdf/2508.03366)

**Industry and practitioner sources:**
- [The Shift from Models to Compound AI Systems — BAIR (Zaharia et al., Feb 2024)](https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/)
- [What Are Compound AI Systems? — IBM](https://www.ibm.com/think/topics/compound-ai-systems)
- [What are Compound AI Systems? — Databricks](https://www.databricks.com/blog/what-are-compound-ai-systems)
- [Constitutional AI — Anthropic research page](https://www.anthropic.com/research/constitutional-ai-harmlessness-from-ai-feedback)
- [Claude's Constitution — Anthropic](https://www.anthropic.com/constitution)
- [The Future Is Neuro-Symbolic — Marcus & Belle, AAAI 2025](https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf)
- [Gary Marcus neurosymbolic PoC guide (internal reference)](../design/marcus-neurosymbolic.md)

---

## What each concept mission must demonstrate

- The mission file is readable by someone unfamiliar with MCL.
- The `.mcl` structure visibly maps to the paper's architecture.
- The mission is runnable against any supported provider (no hardcoded model/key).
- A Judge expert or measurable output makes quality observable, not anecdotal.
- The README explains why this was previously hard and what MCL removes.

---

## Folder structure (target)

```
missions/concepts/
  self-refine/
  debate/
  constitutional-ai/
  mixture-of-agents/
  hybrid-llm-ml/
  llm-as-judge/
  hallucination-reduction/
  verifiable-reasoning/
  compositionality/
```
