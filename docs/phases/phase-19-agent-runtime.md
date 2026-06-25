# Phase 19 — Agent Runtime

**Status:** Done  
**Depends on:** Phase 11 (OCI), Phase 18 (Native AOT)

---

## What this phase delivers

The ability to expose a mission as an OpenAI-compatible HTTP endpoint via `forge serve`.

A user authors a `.mcl` file (the reasoning artifact) and an `agent.yaml` file (the hosting config). `forge serve` reads both and starts a server that accepts `/v1/chat/completions` requests, runs the mission, and streams the response back.

---

## Core design decisions

### An agent is a mission exposed over an endpoint — nothing more

The `agent` concept does not change how a mission works. It does not add new reasoning capabilities. It is purely an infrastructure concern: take an existing `.mcl` file and make it consumable over an HTTP endpoint.

This means:
- The `.mcl` file is authored by the person who designs the thinking model
- The `agent.yaml` file is authored by the person who deploys it
- Those can be the same person, but they don't have to be

An ops team can take someone else's `.mcl` and write their own `agent.yaml` to deploy it — without touching the reasoning artifact.

### One mission per `.mcl` file (explicit constraint)

Every `.mcl` file encodes exactly one thinking model. This is not just an observed pattern — it is a design constraint.

A `.mcl` file is to a mission what an `expert.md` file is to an expert: one file, one unit. Allowing multiple missions per file would pull MCL toward being a module system, which is outside the language's scope.

This constraint makes the agent mapping trivial: one `.mcl` → one mission → one agent → one endpoint. No disambiguation is needed.

### Two files, two concerns

```
missions/build-operator/
  mission.mcl     ← reasoning artifact (experts, thinking model)
  agent.yaml      ← infrastructure artifact (endpoint config)
  mcl.lock        ← resolved expert references
```

**`mission.mcl`** — the language artifact. Declares experts, let bindings, and the mission (thinking model). Unchanged from today.

**`agent.yaml`** — YAML format. References the mission file and carries only hosting concerns. YAML was chosen over a custom format to allow future nesting (auth config, TLS, multiple listeners) and to keep it inspectable by tooling.

Example `agent.yaml`:

```yaml
mission: ./mission.mcl
port: 8080
id: build-operator-v1
```

### `forge serve` is the new CLI verb

```bash
forge serve                  # looks for agent.yaml in current directory
forge serve agent.yaml       # explicit path
```

Mirrors the existing pattern:
- `forge init` activates the OCI grammar in `.mcl`
- `forge run` executes the mission
- `forge serve` activates the agent configuration in `agent.yaml`

### `Katasec.OaiServer` is a separate library

The OAI endpoint plumbing lives in a new library: `katasec/oai-server-dotnet`.

This follows the same pattern as `katasec/oci-client-dotnet`:

| Concern | MCL grammar | CLI verb | Library |
|---------|-------------|----------|---------|
| OCI experts | `from "registry" version "tag"` | `forge init` | `Katasec.OciClient` |
| Agent hosting | `agent.yaml` | `forge serve` | `Katasec.OaiServer` |

`Katasec.OaiServer` owns:
- ASP.NET Core minimal API setup
- `/v1/chat/completions` — non-streaming + SSE streaming (OAI chat format)
- `/v1/models` — model listing
- `/v1/responses` — OpenAI Responses API (2025), non-streaming + SSE streaming
- All request/response shapes via STJ source-gen (AOT-safe)
- Session handling

As of v0.1.4, all three endpoints are spec-compliant and verified by 28 tests using the official `OpenAI` .NET SDK as the test client.

`Katasec.OaiServer` takes an `IChatClient` — it has no knowledge of MCL, missions, or experts. Forge wires a mission-backed `IChatClient` and hands it to the library. The library is independently useful for any `IChatClient` implementation.

### MAF reference

MAF (Microsoft Agent Framework) previously provided this OAI endpoint capability. It was dropped in Phase 18 because it was incompatible with Native AOT. `Katasec.OaiServer` is the AOT-safe replacement, scoped to exactly the capability needed.

Reference: https://learn.microsoft.com/en-us/agent-framework/integrations/openai-endpoints

---

## Open questions

These were identified during design and need to be resolved before implementation begins.

### 1. Session handling — RESOLVED

Agents are long-running conversations, not one-shot API wrappers. Session state must persist across requests.

**Decision: stateful, local file for first pass.**

Session history is stored under `~/.forge/sessions/<session-id>/` — consistent with the existing `~/.forge/` cache pattern. The interface is abstracted so the persistence provider can be swapped without touching the agent runtime:

```csharp
public interface ISessionStore
{
    Task<Session?> GetAsync(string sessionId, CancellationToken ct);
    Task SaveAsync(Session session, CancellationToken ct);
}
```

First implementation: `LocalFileSessionStore` (JSON files under `~/.forge/sessions/`).

**DI note:** `Microsoft.Extensions.DependencyInjection` is AOT-compatible as of .NET 8+. The source generator handles reflection-free registration. Explicit registration works cleanly:

```csharp
services.AddSingleton<ISessionStore, LocalFileSessionStore>();
services.AddSingleton<IOaiServer, OaiAgentHost>();
```

Swapping the persistence provider later is a one-line registration change — nothing else moves. `LocalFileSessionStore` is the right first-pass default; Redis or SQLite can be introduced when multi-instance or cross-restart persistence is needed.

### 2. Input mapping — RESOLVED

**Decision: convention over configuration. No mapping knobs.**

A mission intended to be served as an agent declares `goal` as its single parameter — that is the slot the incoming user message fills. All other inputs are baked into the mission via `let` bindings.

```fsharp
let persona = "Principal SRE, Tekton specialist"   // baked into the mission

mission BuildOperatorDesign(goal) =                // goal = incoming user message
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

`forge serve` reads the mission, sees `goal` as the single parameter, and maps every incoming user message to it. No mapping config, no `inputs:` section in `agent.yaml`, no runtime knobs. The mission file is the single source of truth.

**Design note:** this follows the project's general design philosophy — fewer moving parts, baked-in over dynamic. A mission that needs richer input mapping is a signal to revisit the mission design, not to add flexibility to the agent runtime.

**`agent.yaml` stays minimal:**

```yaml
mission: ./mission.mcl
port: 8080
id: build-operator-v1
```

`id` is the agent's addressable identity — what OAI clients pass in the `model` field of their requests. `Katasec.OaiServer` handles the `id` → OAI `model` field translation internally. Users never interact with the OAI wire format detail.

`id` was chosen over `model` (conflicts with the LLM model `let` binding in `.mcl`) and `name` (ambiguous between display name and identifier).

### 3. Auth — RESOLVED

Two distinct concerns:

**Outbound (forge → LLM provider):** Already solved. `let apiKey = env("MCL_API_KEY")` in the `.mcl` file. No changes needed.

**Inbound (client → forge serve):** Skipped for now — YAGNI. ASP.NET Core's middleware pipeline means auth can be injected later without touching agent logic. No inbound auth in Phase 19.

### 4. Model name advertised — RESOLVED

Closed by the `id` decision in Question 2. The agent advertises itself as the value of `id` in `agent.yaml`. Any `model` field sent by an incoming OAI client request is ignored — the agent knows its own identity from its config. `Katasec.OaiServer` handles this internally.

---

## What is NOT in scope

- Multiple missions per `.mcl` file
- Agent-to-agent communication
- Custom non-OAI protocols
- Persistent session storage (Phase 19 scope is stateless)
- Grammar changes to `.mcl` — the agent declaration lives in `agent.yaml`, not in the MCL language

---

## Implementation sketch (post-design)

1. Create `katasec/oai-server-dotnet` repo
   - AOT-safe ASP.NET Core minimal API
   - OAI request/response POCOs with STJ source-gen
   - SSE streaming
   - Publishes to `nuget.pkg.github.com/katasec`

2. Add `agent.yaml` schema and loader to `ForgeMission.Core`

3. Add `forge serve` command to `ForgeMission.Cli`
   - Reads `agent.yaml`
   - Runs `forge init` check (experts must be resolved)
   - Wires `PipelineRunner` behind an `IChatClient` adapter
   - Hands to `Katasec.OaiServer`

4. Add `Katasec.OaiServer` package reference to `ForgeMission.Cli.csproj`

5. End-to-end test: `forge serve` → Open WebUI → mission runs → response streams back
