# Phase 33 — MclContext: Separating User State from Runtime Metadata

> **Status: Deferred — document only**
> **Priority: Low — no urgent driver; revisit when Phase 31 (Forge Runtime Platform) pulls it forward**
> **Depends on:** Phase 32 (kind:exec), Phase 4 (PipelineRunner)

---

## The problem

MCL's context bag is `Dictionary<string, object>`. It serves two distinct purposes today, but nothing in the type system separates them:

1. **User state** — keys written and read by experts: `cluster_config`, `findings`, `output`, etc. This is shapeless by design; the runtime never needs to know what keys exist.

2. **Runtime metadata** — keys written by `PipelineRunner` to control loop behaviour: `attempt`, `max_loops`, `feedback`. These are internal plumbing, not expert data.

Today both live in the same bag:

```csharp
context["attempt"]   = attempt.ToString();   // runtime plumbing
context["max_loops"] = maxLoops.ToString();  // runtime plumbing
context["feedback"]  = loopFeedback;         // runtime plumbing
context["output"]    = envelope.Text;        // user state
```

This means an expert can accidentally read or overwrite `attempt`. It also means there is no clean place to put runtime-level metadata that experts should never see (execution trace, step timing, etc.).

---

## The middleware analogy

This is the same separation ASP.NET Core makes with `HttpContext`. The host (Kestrel) owns `HttpContext.Connection`, `HttpContext.TraceIdentifier`, and request/response lifecycle. Middleware owns `HttpContext.Request` and `HttpContext.Response`. They share the object but operate on distinct, non-overlapping regions.

MCL needs the same split. The runtime owns metadata; experts own state.

---

## Proposed shape

```csharp
public sealed class MclContext
{
    // Expert-visible state. Shapeless by design — the runtime never inspects keys.
    public Dictionary<string, object> State { get; } = new(StringComparer.Ordinal);

    // Runtime-only metadata. Experts receive MclContext but must not read Runtime.
    // PipelineRunner is the only writer and reader of this section.
    public MissionRuntime Runtime { get; } = new();
}

public sealed class MissionRuntime
{
    public int    Attempt    { get; set; }
    public int    MaxLoops   { get; set; }
    public string Feedback   { get; set; } = "";
    // Future: step trace, timing, cancellation source, active mission name, etc.
}
```

`IExpertRunner` signature becomes:

```csharp
Task<StepEnvelope> RunAsync(ExpertDefinition expert, MclContext context, CancellationToken ct);
```

Experts read and write `context.State` only. `PipelineRunner` reads and writes `context.Runtime` only. The contract is enforced by convention, not the type system — same as ASP.NET Core's split between framework and middleware concerns.

---

## Why this is not urgent

The current `Dictionary<string, object>` works. The namespace collision risk is theoretical today — no expert is likely to accidentally use `attempt` as a key. The refactor touches every runner, every test, and the entire pipeline. That is a large blast radius for a low-urgency problem.

The right trigger for this work is one of:

- **Phase 31 (Forge Runtime Platform)** pulls it forward because the hosted runtime needs richer execution context (tenant, capability version, resource usage) that has no clean home today.
- A real bug caused by runtime key collision with user state.
- A new cross-cutting concern (distributed tracing, step audit log) that clearly belongs in `Runtime` and is ugly to implement without it.

---

## What is NOT the goal

`MclContext` should **not** type the user's state. There is no `MclContext.ClusterConfig` or `MclContext.Findings`. The shapelessness of `State` is what makes unlimited expert composition possible — the same insight as OWIN's `IDictionary<string, object>` environment. Typing it would require the runtime to know what experts put in it, destroying the middleware analogy.

---

## Connection to ExpertDirectory (Phase 32, Spoke 1)

A narrower, immediate version of this problem is that `ExecExpertRunner` needs to know the expert's directory to resolve `./analysis.py`, but the runner contract only receives `ExpertDefinition` and the context bag. The solution there is simpler: add `ExpertDirectory` as a field on `ExpertDefinition`, populated by `ExpertLoader` at parse time. That is a surgical fix and does not require `MclContext`. See Phase 32 Spoke 1.
