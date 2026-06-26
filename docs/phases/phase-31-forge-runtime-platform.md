# Phase 31 — Forge Runtime Platform

> **Status: Design**
> **Depends on:** Phase 19 (forge serve, OaiServer), Phase 25 (forge.toml, two-file model), Phase 29 (UC reference missions — provides the first capabilities to host)
> **Purpose:** Evolve MCL from a developer authoring tool into a platform for publishing,
> hosting, discovering, and consuming reusable reasoning capabilities. This is not an
> extension of `forge serve`. It is a second product built on top of MCL.

---

## Why this phase exists

`forge serve` today exposes a single hosted mission from a developer's local environment.
That is useful, but it still assumes the user is technical: they install Forge, author
missions, configure providers, and run a local process.

This phase targets a different user and a different lifecycle stage.

**The distinction:**

| | Forge CLI | Forge Runtime |
|---|---|---|
| Primary user | Mission author / developer | End user / consumer |
| What they do | Author, validate, test, publish | Discover, select, consume |
| What they see | `.mcl` files, `forge.toml`, experts | A catalog of capabilities |
| Deployment | Local install, AOT binary | Hosted service |
| Analogy | Terraform CLI | HCP Terraform |

End users do not know MCL exists. They connect Open WebUI (or any OAI-compatible
client) to Forge Runtime and see:

```
security-review
pci-audit
k8s-architect
debugger
terraform-review
design-review
```

They pick a capability. The platform handles the rest. The MCL mission underneath
is an implementation detail.

---

## The product split

The clearest analogy is Terraform / HCP Terraform:

| Terraform world | Forge world |
|---|---|
| HCL | MCL |
| Terraform CLI | Forge CLI |
| Terraform module | MCL mission / capability package |
| Terraform Registry | OCI capability registry |
| HCP Terraform | Forge Runtime / Platform |
| Workspace | Hosted capability / agent |
| Run | Mission invocation |
| VCS trigger | Event-driven agent invocation |

Terraform `.tf` files are not what most enterprise users interact with directly.
They interact with workspaces, runs, and outputs in HCP Terraform. MCL `.mcl`
files are not what most end users interact with. They interact with capabilities.

---

## Lifecycle

```
Author
    |
    v
Mission (.mcl file)
    |
    v
Package / Publish
    |
    v
Capability (published artifact in OCI registry)
    |
    v
Host (Forge Runtime registers the capability)
    |
    v
Agent (hosted capability, addressable by name)
    |
    v
Consume (users, tools, events summon the agent)
```

The parallel to modern software delivery:

```
Source → Container Image → Registry → Kubernetes → Users
```

---

## Architecture

### Four-layer model

```
MCL              owns reasoning workflows
Forge CLI        owns local authoring, validation, packaging, publishing
Forge Runtime    owns hosted capabilities, addressable workforce
Orleans          owns runtime identity, activation, lifecycle, collaboration
```

### The layered execution model

```
Experts        execute inside a mission       (MCL PipelineRunner)
Missions       execute inside an agent        (AgentGrain → PipelineRunner)
Agents         collaborate through the runtime (AgentGrain → AgentGrain)
```

**Critical invariant:** Orleans wraps missions; it never decomposes them.

The internal steps of an MCL mission (`Architect -> SecurityReviewer -> Judge`)
execute inside `PipelineRunner` exactly as authored. They are never decomposed into
per-expert grains. The grain's `RunAsync` calls `PipelineRunner` and returns a result.
The workflow is MCL's asset — explicit, readable, reviewable, versionable.

Grain-to-grain calls belong one level higher: a `DesignAgent` that delegates to
`SecurityReviewAgent` and `CostAnalysisAgent` is agent-to-agent collaboration,
not expert-level decomposition.

### Why Orleans and not a dictionary

`Dictionary<string, PipelineRunner>` can technically host multiple missions in one
process. Orleans is not chosen to solve that technical problem.

Orleans is chosen because its virtual actor model expresses the right product abstraction:

| Product concept | Orleans concept |
|---|---|
| Specialist / capability | Grain |
| "Always there, summon on demand" | Virtual actor (activated on first call) |
| Addressable by name | `IGrainWithStringKey` |
| Capability has an identity | `AgentGrain("security-review")` IS the specialist |
| Specialists can collaborate | Grain-to-grain calls |
| Workforce across machines | Silo clustering |

A dictionary is a lookup table. The runtime is a workforce. Orleans expresses that
distinction in code.

### Capability identity vs conversation memory

These are orthogonal concepts and must not be coupled in the grain:

```
AgentGrain("security-review")   = capability (what it knows, how it reasons)
SessionGrain("session-abc123")  = conversation (what was said, to whom, when)
```

A security review capability should not accumulate every previous conversation
simply because it has an Orleans identity. Grain state, if any, should be
minimal — primarily the cached compiled mission representation (parsed AST +
resolved `ExpertDefinition`s). Everything conversational lives in a `SessionGrain`.

This separation gives flexibility: many concurrent sessions per capability,
sessions optionally spanning multiple capabilities, capability state versioned
independently of conversation history.

### AOT boundary

The Forge CLI binary is `PublishAot=true` and must remain so. Orleans is not
AOT-compatible for silo startup. These are different artifacts:

- `forge` (CLI) — Native AOT, used by authors and developers
- `forge-runtime` (Platform) — Regular .NET server process, hosted service

The AOT binary covers `forge run`, `forge validate`, `forge publish`, and
single-mission `forge serve`. The platform binary is a separate deployment artifact.

### OpenAI compatibility as distribution

Forge Runtime exposes capabilities through the OAI-compatible API already implemented
in `Katasec.OaiServer`:

```
GET /v1/models       → capability catalog (grain names)
POST /v1/chat/completions  { "model": "security-review", ... }
                     → routes to AgentGrain("security-review")
```

The OAI protocol is the distribution layer. Any OAI-compatible client (Open WebUI,
Claude Code, any SDK) connects to Forge Runtime without knowing MCL exists. The
user picks a model name; the platform handles the rest.

### MCL's explicit workflow as differentiator

Most AI agents are black boxes. An MCL-backed capability's reasoning is inspectable:
the `.mcl` mission file is readable, reviewable, and versionable. In regulated
contexts (finance, security, compliance), a `pci-audit` capability whose reasoning
model can be audited is a meaningfully different product from an opaque AI wrapper.

---

## Interfaces (proposed)

```csharp
public interface IAgentGrain : IGrainWithStringKey
{
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct);
}

public record AgentRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? SessionId,
    string? CorrelationId,
    bool Stream,
    IReadOnlyDictionary<string, string>? Variables
);

public record AgentResponse(
    string Content,
    AgentStatus Status,
    string? SessionId,
    string? TraceId,
    IReadOnlyList<StepOutput>? StepOutputs,
    string? Error
);
```

The grain's `RunAsync` implementation:
1. Loads/resolves the mission (from grain state cache or disk)
2. Builds context from incoming messages and session history
3. Calls `PipelineRunner.RunAsync` with the resolved mission
4. Returns the final `StepEnvelope` as an `AgentResponse`

---

## Spoke summary

| Spoke | Description | Depends on |
|---|---|---|
| 1 | Platform binary — `ForgeMission.Platform`, Orleans silo, ASP.NET host | Phase 19 OaiServer |
| 2 | Workforce manifest — `forge.toml` `[agents]` section, capability registration at startup | Phase 25 forge.toml |
| 3 | `IAgentGrain` — interface, request/response shapes, wraps `PipelineRunner` | Spoke 1 |
| 4 | OAI API routing — `/v1/models` catalog, `model` field → grain dispatch | Spokes 2, 3 |
| 5 | Capability/Session separation — `AgentGrain` stateless, `SessionGrain` owns conversation | Spoke 3 |
| 6 | Event router — external trigger (webhook/queue) → grain invocation; GitHub PR reference example | Spoke 4 |
| 7 | `forge publish` + OCI capability packaging — package mission as OCI artifact, push to registry | Phase 11 OCI |
| 8 | MVP proof — Phase 29 capabilities hosted, Open WebUI connects, non-technical user validation | Spokes 1–4 + Phase 29 |

---

## Hub + Spokes

### Spoke 1 — Platform Binary

Separate project: `ForgeMission.Platform` (or `forge-runtime`). Not AOT.

- Orleans silo configured in-process
- ASP.NET Core host with `Katasec.OaiServer` wired in
- `forge-runtime serve` CLI verb (or standalone `forge-runtime` binary)
- `forge serve --runtime orleans` flag as an alternative entry point

The grain factory is the bridge: OAI request arrives → extract `model` field →
`grainFactory.GetGrain<IAgentGrain>(model)` → `RunAsync`.

### Spoke 2 — Workforce Manifest

Extend `forge.toml` with an `[agents]` section:

```toml
[agents]
"security-review"   = "./missions/security-review/mission.mcl"
"pci-audit"         = "./missions/pci-audit/mission.mcl"
"k8s-architect"     = "./missions/k8s-architect/mission.mcl"
"debugger"          = "./missions/debugger/mission.mcl"
```

At silo startup, each entry is registered as a named grain. `/v1/models` returns
all keys. Grain activation loads the corresponding `.mcl` file and caches the
compiled mission (parsed AST + resolved `ExpertDefinition`s). Reload on
`forge-runtime reload <name>` or grain deactivation.

### Spoke 3 — `IAgentGrain`

Minimal grain that wraps `PipelineRunner`. Grain state: compiled mission cache only.

The grain does NOT decompose the mission into sub-grains. It calls `PipelineRunner`
as a single unit of work. The mission's internal steps are MCL's concern.

### Spoke 4 — OAI API Routing

`Katasec.OaiServer` already handles OAI wire format. This spoke wires the grain
factory into the server's `IChatClient` position:

- `/v1/models` → list grain keys from manifest
- `POST /v1/chat/completions` → `model` field → `AgentGrain(model).RunAsync`
- Streaming: grain returns result; SSE framing is ASP.NET layer concern

### Spoke 5 — Capability/Session Separation

Phase 1: `AgentGrain` is effectively stateless per request (caches compiled mission only).
Session history handled by existing `ISessionStore` if present.

Phase 2: Introduce `SessionGrain(sessionId)` that owns message history. `AgentGrain`
retrieves session context from `SessionGrain` at the start of each `RunAsync` call and
updates it at completion.

The grain key of the `SessionGrain` is scoped to a user + capability:
`{agentName}/{sessionId}` avoids cross-agent session bleed.

### Spoke 6 — Event Router

An event arrives (GitHub webhook, queue message, scheduled trigger) and summons a
hosted capability.

```
External event
    |
    v
Event Router (ASP.NET middleware or separate listener)
    |
    v
Resolve target agent from event type + config
    |
    v
AgentGrain(agentName).RunAsync(request)
    |
    v
Deliver result (post to webhook response URL, write to output, etc.)
```

MCL is the workflow. Events are triggers. The router maps event types to agent names
via a simple config block — no MCL grammar changes needed.

Reference example: GitHub PR opened → `AgentGrain("code-reviewer")` → post review comment.

### Spoke 7 — `forge publish` + OCI Capability Packaging

Package a mission + its resolved experts as a single OCI artifact:

```bash
forge publish security-review:v1.0 --registry ghcr.io/myorg
```

The runtime discovers and pulls capabilities from the registry:

```toml
[agents]
"security-review" = "ghcr.io/myorg/security-review:v1.0"
```

This closes the full lifecycle: Author → Mission → `forge publish` → OCI registry →
Forge Runtime pulls → capability available in workforce.

The OCI infrastructure from Phase 11 (expert distribution) is reused.

### Spoke 8 — MVP Proof

**Definition of done for the platform concept:**

1. Forge Runtime starts with the Phase 29 UC missions registered as capabilities
2. Open WebUI is configured to point at Forge Runtime instead of GPT / Claude
3. A non-technical user opens Open WebUI, sees the capability list, picks one, sends a message
4. They receive a useful response without knowing MCL, `forge`, or Orleans exist

This is the moment the product split is validated. Everything else in this phase
is infrastructure to make that moment possible.

---

## What is NOT in scope

- Per-expert Orleans grains — missions execute inside `PipelineRunner` unchanged
- Multi-cluster deployment — single silo first
- Temporal / durable workflows — not needed at this stage
- SaaS / multi-tenant platform — self-hosted team platform is the first target
- Inbound auth on the OAI endpoint — YAGNI, ASP.NET middleware can add this later
- `debate {}` block — addressed in a future phase once agent-to-agent collaboration
  is validated at the grain level

---

## Open design questions

| # | Question | Notes |
|---|---|---|
| 1 | Who hosts the silo? Single developer machine, team server, cloud VM? | Self-hosted team platform is the first target. Managed hosting is a later product decision. |
| 2 | Should `forge-runtime` be a separate binary or a mode of `forge`? | Separate binary keeps the AOT `forge` clean. Mode flag (`forge serve --runtime orleans`) is simpler operationally. |
| 3 | How are provider credentials (`apiKey`, `model`) configured in the platform? | `forge.toml` provider profiles already handle this. The platform inherits them. |
| 4 | How does the OCI capability registry interact with the workforce manifest? | Spoke 7 scope — `[agents]` can reference both local paths and OCI URIs. |
| 5 | Should `/v1/models` return capability metadata (description, version) beyond the name? | Useful for Open WebUI display. Could be sourced from `agent.yaml` or the OCI artifact label. |
