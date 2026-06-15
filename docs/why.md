# Why Mission Control Language exists

## Where it came from

MCL emerged from six months of real-world LLM usage across meaningfully different problem
domains: production debugging on custom applications, understanding and debugging complex
infrastructure defined in IaC, Kubernetes and Helm operations, and software development
across multiple languages and stacks.

In every case, getting reliable output required the same manual work: decompose the problem,
identify the relevant reasoning lenses, sequence them deliberately, and structure the handoff
between them. That process was always implicit — buried in ad-hoc prompts, markdown files,
and trial and error.

MCL is the codification of that process. It makes the reasoning structure explicit, named,
composable, and reviewable.

## Core thesis

Large language models perform best when reasoning is constrained through deliberate
decomposition and the application of expertise.

A single general-purpose prompt asks the model to architect, review, challenge, and conclude
simultaneously. Expert composition asks each lens to do one thing well, in sequence, with the
previous output as input.

**A mission is a reasoning structure, not an execution plan.**

## Why explicit reasoning structures matter

Most AI tooling expresses reasoning through prompts, markdown instructions, YAML, or agent
configuration. This works at small scale, but the result is:

- **ambiguous** — intent is buried in prose, not structure
- **not composable** — reasoning patterns cannot be named, reused, or shared
- **not reviewable** — a human or oversight agent cannot inspect the reasoning approach, only the output
- **not handoff-friendly** — a fresh agent session cannot reconstruct how a problem was being reasoned about
- **execution-focused** — tool calls, retries, model selection get more attention than the reasoning itself

MCL addresses all of these by making the reasoning structure a first-class artifact.

## The broader methodology

MCL does not stand alone. It is one layer in a deliberate engineering approach to LLM-driven work:

1. **Design first** — never execute cold. Iterate on design until it is solid before any implementation begins.
2. **Phase decomposition** — break the design into agreed phases. Each phase is a meaningful, bounded unit of work.
3. **Atomic task generation** — per phase, generate tasks in sequential dependency order so each can be executed and tested before the next begins.
4. **Narrow execution** — by the time an agent executes, the work is so well-prescribed that there is little room for drift.
5. **Oversight** — an architect agent reviews the work of the executing agent and enforces quality gates.
6. **Session continuity** — agent performance degrades as context fills. Sessions are treated as bounded units with structured handoffs.

MCL addresses the reasoning structure layer: giving agents a clear, reviewable definition of
how to approach a problem.

## Thinking models MCL can express

The same language expresses many common reasoning structures through expert composition.

### Progressive refinement
```fsharp
mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

### Adversarial review
```fsharp
mission ReviewArchitecture =
    Architect
    |> Skeptic
    |> RiskReviewer
    |> PrincipalReviewer
```

### Scientific method
```fsharp
mission ValidateIdea =
    HypothesisBuilder
    |> ExperimentDesigner
    |> EvidenceReviewer
    |> ConclusionWriter
```

### OODA loop
```fsharp
mission RespondToIncident =
    Observer
    |> Orienter
    |> DecisionMaker
    |> Remediator
```

### Separation of concerns
```fsharp
mission DesignPlatform =
    PlatformArchitect
    |> SecurityReviewer
    |> CostReviewer
    |> ReliabilityReviewer
```

### Tradeoff analysis
```fsharp
mission ChooseArchitecture =
    OptionGenerator
    |> TradeoffAnalyst
    |> ScenarioModeler
    |> DecisionAdvisor
```

## Testable hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a
> single general-purpose prompt.

The `build-operator` mission tests this. Findings are documented in [`findings.md`](findings.md).
