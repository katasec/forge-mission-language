# AGENTS.md — Operating Instructions for FML

This file tells you how to work on this repository. Read it before doing anything else.

---

## What this project is

Forge Mission Language (FML) is a minimal language for expressing structured reasoning through the composition of experts. Three primitives: `mission`, `expert`, `|>`. See [README.md](README.md) for the full picture and [docs/design/language.md](docs/design/language.md) for the grammar and syntax decisions.

---

## How to orient at the start of a session

1. Read this file
2. Read [docs/plan.md](docs/plan.md) — the hub. It tells you what phases exist, which are done, and which are active
3. Read the spoke doc for the current phase — linked from `docs/plan.md` — to see which tasks are done and which are next
4. Read [docs/design/architecture.md](docs/design/architecture.md) if you need to understand component boundaries

Do not load everything at once. Start from the hub and follow links only when the task requires it.

---

## How work is structured

Work follows a strict methodology. Do not deviate from it.

### Design first
All design decisions are captured in `docs/design/` before implementation begins. If something is unclear, check there first. If it is not documented, raise it before implementing.

### Phases
Work is broken into phases. Each phase has a spoke document in `docs/phases/`. Phases must be completed in order — each phase produces something independently testable before the next begins.

### Tasks
Each phase doc contains a task table with statuses. Tasks within a phase must be done in the order listed — they are in sequential dependency order.

### Completion conditions
Each phase doc defines a completion condition. Do not mark a phase Done in `docs/plan.md` until that condition is met.

---

## How to update status

When you start a task, update its status in the phase spoke doc from `Not Started` to `In Progress`.

When you complete a task, update it to `Done`.

When all tasks in a phase are done and the completion condition is met:
- Update the phase spoke doc with a `## Result` section summarising what was built and test outcomes
- Update `docs/plan.md` to mark the phase `Done`
- Commit and push

Status values: `Not Started` | `In Progress` | `Done`

---

## How to run the build and tests

```bash
dotnet build src/ForgeMission.slnx
dotnet test src/ForgeMission.slnx
```

All tests must pass before marking any task complete. Never mark a task done if tests are failing.

---

## Project structure

```
README.md               — what FML is and why it exists
AGENTS.md               — this file
docs/
  plan.md               — hub: phase list with statuses and links
  design/               — design decisions (language, architecture, MAF research, methodology)
  phases/               — one spoke per phase with task lists and statuses
src/
  ForgeMission.Core/    — parser, expert loader, pipeline runner, MAF adapter
  ForgeMission.Cli/     — CLI entry point
  ForgeMission.Tests/   — xUnit tests
examples/               — example missions (build-operator added in Phase 6)
runs/                   — gitignored, output of fml run
```

---

## Architecture — the short version

```
CLI
 └→ Pipeline Runner
      └→ Parser           (pure C#, no dependencies)
      └→ Expert Loader    (resolves markdown files)
      └→ IExpertRunner
           └→ MAF Adapter (only file that touches Microsoft Agent Framework)
```

MAF is an internal implementation detail. It must not appear above the adapter layer. See [docs/design/architecture.md](docs/design/architecture.md) for full detail.

---

## Conventions

- **No Co-Authored-By lines in commits.** Commits are attributed to the repo owner only.
- **PascalCase for expert and mission names.** Enforced by the parser — lowercase identifiers are a parse error.
- **Lowercase keywords** (`mission`, `expert`). These are part of the language, not user-defined names.
- **No business logic in the CLI.** The CLI wires up dependencies and delegates to Core. Nothing else.
- **MAF stays behind `IExpertRunner`.** The parser, AST, pipeline runner, and CLI must have zero knowledge of MAF.

---

## At the end of a session

Before finishing:
1. Ensure all completed tasks are marked `Done` in the relevant phase spoke doc
2. Ensure `docs/plan.md` reflects the current phase status accurately
3. Ensure all tests pass
4. Commit and push any outstanding changes

Leave the hub small and current so the next agent can orient quickly.
