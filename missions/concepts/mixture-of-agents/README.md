# Mixture of Agents

## 1. Foundational concept

Mixture of Agents (MoA) is a technique in which multiple LLM agents process a problem
in layers, with each agent receiving the outputs of prior agents as additional context.
The key property is "collaborativeness": even agents that are individually weaker
produce better output when given other agents' responses as reference material.

Wang et al. (2024) state: *"We find that LLMs tend to generate better responses when
presented with outputs from other models, even if those models are less capable."* This
collaborativeness property, combined with role specialisation across layers, allows a
composition of medium-sized models to surpass larger monolithic models on standard
benchmarks.

MCL's sequential pipeline (A → B → C → D) is the natural expression of MoA: each
expert receives the full accumulated context from all prior experts. The pipeline file
IS the agent layering architecture.

## 2. References

**Papers:**
- Wang, J., et al. — "Mixture-of-Agents Enhances Large Language Model Capabilities" —
  2024 — https://arxiv.org/abs/2406.04692
  Key result: a MoA of medium-sized models surpassed GPT-4 Omni on AlpacaEval 2.0
  (65.1% vs 57.5%), FLASK, and MT-Bench. The gain comes from collaborativeness and
  role specialisation across layers.
- Wang, J., et al. — "Towards Generalized Routing: Model and Agent Orchestration" —
  2024 — https://arxiv.org/html/2509.07571v1
  Extends MoA with generalised routing across heterogeneous model types.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  *"State-of-the-art AI results are increasingly obtained by compound systems with
  multiple components, not just monolithic models."* MoA is a primary example.
- IBM — "What Are Compound AI Systems?" —
  https://www.ibm.com/think/topics/compound-ai-systems

## 3. How MCL demonstrates this

```
// Mixture of Agents — sequential layered accumulation
// Wang et al., 2024 (https://arxiv.org/abs/2406.04692)

let problem = "Design a distributed rate limiter for an API gateway that must
handle 100,000 requests per second across 50 global regions, with per-user
quotas, burst allowances, and graceful degradation when the rate limiter
itself is unavailable"

mission MixtureOfAgents(problem) = {
    Analyst
    -> Strategist
    -> Implementer
    -> Reviewer
}

output(MixtureOfAgents)
```

Each `->` is a layer in Wang et al.'s MoA architecture. Each expert receives the
full accumulated conversation — the analysis, strategy, and implementation plan all
flow forward to every subsequent stage. The `Reviewer` sees everything the other three
produced. This is the collaborativeness property: each layer enriches the context that
the next layer reads.

### Example A — the paper's own benchmark task domain

Wang et al. validated MoA on AlpacaEval 2.0 and MT-Bench — benchmarks of complex,
multi-turn reasoning tasks. Complex system design questions are precisely the category
MT-Bench evaluates: multi-step reasoning requiring both technical depth and synthesis.

This mission applies MoA to a distributed systems design problem — the class of task
where multi-layered expert accumulation adds the most value:

```
Analyst → Strategist → Implementer → Reviewer
```

- **Analyst:** decomposes the problem into requirements, constraints, and tensions
- **Strategist:** receives the analysis; proposes an approach that resolves the tensions
- **Implementer:** receives both; translates strategy into concrete technical decisions
- **Reviewer:** receives all three; closes gaps and surfaces the key insight

Each expert sees the full accumulated output of every prior expert. This is the MoA
collaborativeness property made visible in the mission file.

**What MCL adds over the paper:** Wang et al. used homogeneous agents (identical prompts,
different model instances). MCL uses role-specialised experts — each with a distinct
mandate. Role-differentiated MoA is the extension the paper identifies as an open area.

### Example B — general business problem

Replace the `problem` variable with any complex decision:
- Scaling a customer support operation to handle 10× current volume
- Designing a data governance framework for a regulated industry
- Planning a technology migration with no downtime requirement

The four roles (Analyst / Strategist / Implementer / Reviewer) apply to almost any
complex problem. Each stage adds a layer of depth that the prior stage could not provide
alone — the accumulation property the paper identifies as the source of MoA's gains.

## 4. Why this is normally hard

Without MCL, implementing a sequential multi-agent accumulation pipeline requires:
- Four sequential LLM API calls
- Passing the accumulated context correctly to each call (concatenating prior outputs)
- Maintaining a conversation state object that grows with each stage
- Formatting each stage's output for the next stage's input
- Role-specific system prompts stored and managed separately

In LangChain, this requires a `SequentialChain` with memory, custom output parsers, and
careful context window management. The pipeline logic is in Python, separate from the
role definitions. Adding a fifth expert or reordering stages requires code changes.

**With MCL** (the full file is shown at the top of this section):

Adding a fifth expert is one line. Each expert's role is in its own `expert.md` file —
readable and editable by a domain expert without touching the pipeline. The context
accumulation is handled by the runtime.

## Setup

```bash
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic
forge run --mission MixtureOfAgents
```

To try a different problem, edit the `let problem` line in `mission.mcl`.
