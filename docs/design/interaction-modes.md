# Interaction Modes & the Classifier-Router Pattern

## The problem

Current AI agent harnesses treat every human input as a task to execute. They don't.

Real human-AI collaboration has distinct modes — and conflating them degrades the quality of every one:

| Mode | What it looks like |
|------|--------------------|
| **Discovery** | Human needs to understand something — asks questions, seeks explanation, wants the AI to research and synthesise |
| **Design** | Iterative exploration — options compared, decisions made, tradeoffs reasoned through |
| **Planning** | Organising work — hub/spoke docs updated, priorities set, phases sequenced |
| **Execution** | Imperative tasks — build this, document that, commit and push |
| **Research** | External lookup — find current state, check online sources, use findings to inform the next decision |

A developer in a long multi-session effort moves fluidly across all five. An agent harness that only knows how to execute makes the human do all the mode-switching overhead manually — or worse, treats a design question as a task and produces the wrong thing entirely.

## The insight: classifier as router

The fix is a **Classifier expert** at the top of every reasoning chain. Its job is to identify which mode the current input represents and route accordingly — like HAProxy routing HTTP traffic to the right backend based on the request.

```
Human input
     ↓
 Classifier         ← what mode is this?
     ↓
 ┌───┴────────────────────────────┐
 │                                │
Discovery        Design       Execution
  Expert          Expert        Chain
```

This does three things that aren't obvious at first:

1. **Each expert gets clean context.** The Developer never sees the Discovery conversation. The Architect never sees test output. Every expert operates on exactly the context relevant to their role.

2. **The top-level session stays coherent.** Mode-switching happens inside the mission, transparently, without polluting the shared context.

3. **Behaviour becomes codifiable.** Once modes are explicit, you can define exactly how each one should behave — what experts engage, in what order, with what constraints.

## The SDLC meta-mission

A software development lifecycle expressed as a routing mission. `Classifier` (stdlib) identifies the mode; `when()` guards ensure only the relevant experts engage; Planner checkpoints after each phase.

```fsharp
mission SDLCAgent(input) = {
    Classifier
    -> DesignWorkflow(input: input)    when(mode: "design")
    -> TaskWorkflow(input: input)      when(mode: "task")
    -> DiscoveryWorkflow(input: input) when(mode: "discovery")
    -> ResearchWorkflow(input: input)  when(mode: "research")
    -> Planner                         when(else)
}
```

Each `*Workflow` is itself a mission — independently testable and publishable. The routing mission is a pure table-of-contents. Context pollution between modes is eliminated because each mode mission has its own isolated context.

The inline form is also valid — useful when a mode's pipeline is short:

```fsharp
mission SDLCAgent(input) = {
    ProductManager                               // understands intent, frames the problem
    -> Classifier                                // identifies mode: task, design, discovery, research
    -> Planner                                   // updates hub/spoke before any work begins
    -> Architect when(mode: "design")            // engages only for design conversations
    -> Planner   when(mode: "design")            // records design decisions
    -> Developer when(mode: "task")              // engages only for execution tasks
    -> Planner   when(mode: "task")              // records what was built
    -> Tester    when(mode: "task")              // verifies execution output
    -> Planner   when(mode: "task")              // records test results
    -> Releaser  when(mode: "task")              // ships if ready
    -> Planner                                   // final context checkpoint
}
```

The Planner is woven throughout — not just at the end — because context organisation is not a final step, it is a discipline enforced at every phase boundary.

## Language primitive: `when()`

`when()` is an MCL step guard. It makes a step conditional on a context bag value set by a prior step.

```fsharp
-> Architect when(mode: "design")
-> Developer when(mode: "task")
-> Planner   when(else)
```

**Semantics:**
- `when(key: value)` — step runs only if the context bag key matches the value exactly (Phase 25: string equality only)
- `when(else)` — explicit default branch; runs if no other guard matched
- **Hard error** if nothing matches and no `when(else)` is present — silent skip would mean missing routing logic is invisible
- Unmatched steps log at `--verbose` only
- Mode is set by the preceding `Classifier` step — emitted as a structured field in the `StepEnvelope`, injected into the context bag

This is the minimal conditional needed to express routing — not general branching, not match expressions, just step-level guards on context bag values. Richer expressions (`>`, `or`, `contains`) are deferred until the typed context bag lands (Phase 22).

## General form of the classifier-router pattern

```fsharp
mission AnyComplexAgent(input) = {
    Classifier
    -> ExpertA(input: input) when(mode: "a")
    -> ExpertB(input: input) when(mode: "b")
    -> ExpertC(input: input) when(mode: "c")
    -> FallbackExpert        when(else)
}
```

The mission codifies not just *how* to reason but *what kind of reasoning is needed* — and routes accordingly. This is the difference between an agent that executes instructions and one that genuinely collaborates.

## Why this matters beyond SDLC

The classifier-router pattern is not specific to software development. Any complex, multi-session human-AI collaboration has mode boundaries. The pattern:

1. Surfaces which mode an interaction belongs to (classifier as a named reasoning act)
2. Eliminates context pollution between modes (mission composition isolates context)
3. Makes each mode testable in isolation (each branch mission runs standalone)
4. Makes routing behaviour inspectable (the routing mission is a readable table of contents)

## Standard library: `Classifier`

`Classifier` ships embedded in the `forge` binary. It passes the four stdlib gates:

- Gate 1 — pipeline mechanics: identifies *which mode* to invoke, not *what to do in* that mode
- Gate 2 — universal: every routing mission needs it
- Gate 3 — canonical: `when()` routing depends on a consistent routing signal; incompatible implementations break composition
- Gate 4 — freezable: "determine which mode this input is" is stable enough to commit to

Domain-specific classifiers (e.g., "is this a legal or financial query?") are OCI — they embed domain knowledge and therefore fail Gate 1.

## Origin

This pattern emerged from direct experience with long multi-session AI-assisted work — extended sessions where context entropy, mode confusion, and lack of structured routing degraded response quality over time. The hub/spoke documentation pattern, the Session Continuity Protocol, and MCL itself all emerged from the same source. The classifier-router is the runtime complement to those structural solutions.
