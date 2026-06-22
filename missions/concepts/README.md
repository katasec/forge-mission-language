# Concept Missions

Researchers studying how to improve LLM reasoning have converged on a set of
architectural patterns — iterative self-critique, multi-agent debate, constitutional
principles, layered agent accumulation, symbolic grounding — that reliably outperform
single-shot prompting. Each pattern has been validated in peer-reviewed work.

These missions express each pattern as a runnable MCL file. The goal is to show that
the architecture described in the paper and the structure of the mission file are the
same artifact — readable by the domain expert who understands the problem, not just
the engineer who builds the system.

| Concept | Mission | Primary source |
|---|---|---|
| Self-Refine — generate, critique, revise iteratively | [self-refine/](self-refine/) | [Madaan et al., NeurIPS 2023](https://arxiv.org/abs/2303.17651) |
| Multi-Agent Debate — independent perspectives converging | [debate/](debate/) | [Du et al., 2023](https://arxiv.org/abs/2401.05998) |
| Constitutional AI — critique against explicit named principles | [constitutional-ai/](constitutional-ai/) | [Bai et al., Anthropic 2022](https://arxiv.org/pdf/2212.08073) |
| Mixture of Agents — sequential layered expert accumulation | [mixture-of-agents/](mixture-of-agents/) | [Wang et al., 2024](https://arxiv.org/abs/2406.04692) |
| Hybrid LLM + Classical ML — heterogeneous expert composition | [hybrid-llm-ml/](hybrid-llm-ml/) | [CoE, 2024](https://arxiv.org/pdf/2412.01868) · [Marcus & Belle, AAAI 2025](https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf) |
| LLM-as-Judge — reference-guided quality evaluation | [llm-as-judge/](llm-as-judge/) | [Zheng et al., 2023](https://arxiv.org/abs/2306.05685) |
| Hallucination Reduction — symbolic rules as correctness gates | [hallucination-reduction/](hallucination-reduction/) | [RLSF, 2024](https://arxiv.org/pdf/2405.16661) |
| Verifiable Reasoning — generate steps, verify symbolically | [verifiable-reasoning/](verifiable-reasoning/) | [AlphaGeometry, DeepMind 2024](https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/) |
| Compositionality — novel tasks from known primitives | [compositionality/](compositionality/) | [Dziri et al., NeurIPS 2024](https://arxiv.org/abs/2307.05471) · [BAIR, 2024](https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/) |

Each mission folder contains a `README.md` that covers the foundational concept,
the research that validates it, and how MCL expresses it — including the full
`mission.mcl` so the architecture is visible at a glance.
