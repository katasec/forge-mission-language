# Phase 27 — Project Assistant Missions

## Status: Design

## The vision

MCL manages its own development using MCL.

A `software-project-assistant` mission sits behind a `forge serve` endpoint.
Claude Code (or any OAI-compatible client) points at it as a custom API base URL.
Requests are intercepted, routed through the right expert chain, and returned as
structured markdown. The client never knows it's talking to a mission.

```
Claude Code
    │  POST /v1/chat/completions
    ▼
forge agent  (OAI-compatible endpoint, port 8080)
    │
    ▼
SoftwareProjectAssistant mission
    │  SoftwareRequestClassifier routes the request
    ├──► ArchitectMode       when(output: "architecture")
    ├──► DevelopmentMode     when(output: "development")
    └──► ProjectAssistant    when(output: "project")   ← status / next / handoff / docs
    │
    ▼
Actual LLM  (OpenAI / Anthropic / Ollama)
```

## Three missions

### Layer 1 — `missions/project-assistant/`

Generic. Works for any project that uses hub/spoke documentation.

```fsharp
mission ProjectAssistant(request, plan, phases) = {
    RequestClassifier
    -> StatusReporter    when(output: "status")    // current phase/spoke summary
    -> NextStepAdvisor   when(output: "next")      // what to tackle next, with rationale
    -> HandoffGenerator  when(output: "handoff")   // session continuity prompt, paste-ready
    -> DocUpdater        when(output: "document")  // which spoke to update + what to write
    -> GeneralAdvisor    when(else)
}
```

### Layer 2 — `missions/software-project-assistant/`

Extends with architecture and development modes. Composes `ProjectAssistant`
as a step for everything project-management-related.

```fsharp
mission ArchitectMode(request, plan, codebase) loop(2) = {
    SoftwareArchitect
    -> ArchitectureReviewer
    -> ArchitectureDocumenter
    -> QualityJudge
}

mission DevelopmentMode(request, plan, codebase) = {
    SoftwareDeveloper
    -> CodeReviewer
    -> TestAdvisor
}

mission SoftwareProjectAssistant(request, plan, phases, codebase) = {
    SoftwareRequestClassifier
    -> ArchitectMode(request: request, plan: plan, codebase: codebase)       when(output: "architecture")
    -> DevelopmentMode(request: request, plan: plan, codebase: codebase)     when(output: "development")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)        when(output: "project")
}
```

### Layer 3 — `missions/product-owner-assistant/`

Different role, same base — reuses `ProjectAssistant` unchanged for all
project-management requests.

```fsharp
mission ProductOwnerAssistant(request, plan, phases, backlog) = {
    ProductRequestClassifier
    -> UserStoryWriter(backlog: backlog)                                   when(output: "story")
    -> BacklogPrioritizer(backlog: backlog)                                when(output: "backlog")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)     when(output: "project")
}
```

## Experts to build

### `project-assistant` experts

| Expert | Role |
|--------|------|
| `RequestClassifier` | Reads `{{request}}` and emits routing keyword: status / next / handoff / document |
| `StatusReporter` | Reads `{{plan}}` + `{{phases}}`, produces current state summary markdown |
| `NextStepAdvisor` | Reads `{{plan}}` + `{{phases}}`, outputs: what to do next and why |
| `HandoffGenerator` | Produces a paste-ready session continuity summary from plan + phases |
| `DocUpdater` | Identifies which spoke to update and what to write |
| `GeneralAdvisor` | Fallback for anything the classifier doesn't recognise |

### `software-project-assistant` additional experts

| Expert | Role |
|--------|------|
| `SoftwareRequestClassifier` | Routes: architecture / development / project |
| `SoftwareArchitect` | Produces architecture guidance from `{{request}}` + `{{codebase}}` |
| `ArchitectureReviewer` | Critiques the architecture — identifies gaps and risks |
| `ArchitectureDocumenter` | Writes an ADR-style record of the decision |
| `QualityJudge` | Passes or fails the architecture — triggers loop retry if not production-ready |
| `SoftwareDeveloper` | Implementation guidance with code, testing strategy, edge cases |
| `CodeReviewer` | Critiques the implementation plan |
| `TestAdvisor` | Suggests test strategy and coverage approach |

## Context injection — how experts read project files

Until tool calling lands (Phase 22), a thin shell wrapper injects file contents as env vars:

```bash
#!/usr/bin/env bash
# run.sh — software-project-assistant launcher
export MCL_REQUEST="$1"
export MCL_PLAN="$(cat docs/plan.md 2>/dev/null || echo '')"
export MCL_PHASES="$(cat docs/phases/*.md 2>/dev/null | head -300 || echo '')"
export MCL_CODEBASE="$(git log --oneline -20 2>/dev/null || echo '')"

forge run missions/software-project-assistant/mission.mcl \
  --var request="$MCL_REQUEST" \
  --var plan="$MCL_PLAN" \
  --var phases="$MCL_PHASES" \
  --var codebase="$MCL_CODEBASE"
```

When Phase 22 (tool calling) lands, `run.sh` disappears — experts read files directly.

## Serving behind Claude Code

### agent.yaml

```yaml
mission: ../../missions/software-project-assistant/mission.mcl
port: 8080
id: sw-project-assistant-v1
```

### Start the agent

```bash
forge agent start --agent-file agents/sw-project-assistant/agent.yaml
```

### Point Claude Code at it

In the project's `.claude/settings.json` (or `settings.local.json` for personal use):

```json
{
  "env": {
    "ANTHROPIC_BASE_URL": "http://localhost:8080/v1"
  }
}
```

Claude Code now routes every request through the forge agent. The mission's
`SoftwareRequestClassifier` intercepts, routes to the right expert chain, and
returns structured markdown. Claude Code sees a normal LLM response.

### What each Claude Code prompt triggers

| Claude Code input | Classifier output | Expert chain that runs |
|---|---|---|
| "what's next?" | `project` → `next` | `NextStepAdvisor` (reads plan + phases) |
| "write a handoff" | `project` → `handoff` | `HandoffGenerator` |
| "design the auth service" | `architecture` | `ArchitectMode` loop(2) |
| "implement the user model" | `development` | `DevelopmentMode` |
| "update the spoke doc" | `project` → `document` | `DocUpdater` |

## Build order

1. **Build `project-assistant`** — flat mission, no composition needed, runnable now
   - All 6 experts + `run.sh` + `agent.yaml`
   - Test: `./run.sh "what's next?"` and `./run.sh "write a handoff"`
   - Serve behind Claude Code and validate the interception works

2. **Build `software-project-assistant` flat version** — inline experts, no sub-missions
   - Add `SoftwareRequestClassifier` + software-specific experts
   - `ArchitectMode` and `DevelopmentMode` as flat sequences (no loop yet)
   - Test all routing branches through Claude Code

3. **Refactor to composed version** — once Mission Composition phase lands
   - Extract `ArchitectMode` and `DevelopmentMode` as proper sub-missions
   - Add `loop(2)` to `ArchitectMode`
   - `ProjectAssistant` becomes a proper sub-mission step

4. **Build `product-owner-assistant`** — after composition lands

## What's already built

- `forge serve` — OAI-compatible endpoint (Phase 19) ✓
- `forge agent start` — Docker container mode (Phase 23) ✓
- Claude Code → forge agent integration — proven in Phase 24 ✓
- `when(output: "x")` routing — Phase 25 Spoke 1 ✓
- `when(else)` fallback — Phase 25 Spoke 1 ✓

## What's blocked

- Full composed version (`ProjectAssistant` as a sub-mission step) — needs Mission Composition phase
- `loop(2)` on `ArchitectMode` sub-mission — needs Mission Composition phase
- Experts reading files autonomously — needs Phase 22 (tool calling)

## Self-hosting note

Once `project-assistant` is running behind Claude Code on this repo, MCL is
self-hosting its own development workflow. The "what's next?" question that has
been answered manually in every session becomes a mission invocation.

That is the demonstration.
