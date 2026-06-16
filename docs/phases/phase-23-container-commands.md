# Phase 23 — Container Commands (HUB)

**Status:** Design  
**Depends on:** Phase 19 (Agent Runtime — `forge serve`, `agent.yaml`)

## Goal

Add `forge agent` and `forge webui` command groups so a developer can start an MCL
agent and a pre-configured Open WebUI instance in Docker with two commands, from
the directory containing their `agent.yaml`.

```
forge agent start    # run forge serve inside a container
forge agent stop
forge webui start    # run open-webui configured to talk to the agent
forge webui stop
```

---

## Decisions (final — do not re-litigate in spokes)

### Docker integration: Process.Start, not Docker.DotNet

`Docker.DotNet` uses `Newtonsoft.Json` internally — not AOT-safe. We shell out
to the `docker` CLI via `Process.Start`. String parsing only; no typed Docker SDK.

### TUI: Spectre.Console (AOT-safe subset only)

Use Spectre.Console for prereq table output and status messages.  
**AOT constraint:** use only table/markup string primitives. Do NOT use
`AnsiConsole.Render<T>`, `IRenderable` reflection, or any type-based prompt APIs.
These crash under AOT. Stick to `AnsiConsole.Write(new Table(...))` and
`AnsiConsole.MarkupLine(...)`.

### Shared prereq checker

Both `forge agent start` and `forge webui start` run the same checker.
The checker accepts a list of checks to run; callers compose the relevant subset.

### Docker networking

A bridge network named `forge-net` is created on demand.
Containers resolve each other by name within that network.

```
forge-net (bridge)
  ├── forge-agent-{id}   port {port}   (from agent.yaml id + port)
  └── open-webui         port 8080     (mapped to host 3000)
```

### Agent container image: `forge:local`

A `Dockerfile` at the repo root builds a minimal Debian-slim image with the
pre-built `forge-linux-x64` binary. `forge agent start` checks whether
`forge:local` exists locally; if not, builds it via `docker build`.

```dockerfile
FROM debian:bookworm-slim
COPY forge-linux-x64 /usr/local/bin/forge
RUN chmod +x /usr/local/bin/forge
ENTRYPOINT ["forge"]
```

`forge-linux-x64` must be present alongside the Dockerfile. In the release
workflow this is the published artifact; locally it is the output of `make build-linux`.

### Agent container invocation

```
docker run -d
  --name forge-agent-{id}
  --network forge-net
  -p {port}:{port}
  -v {cwd}:/workspace
  -e MCL_API_KEY
  -e MCL_MODEL
  -e MCL_PROVIDER
  -e MCL_ENDPOINT
  forge:local serve --agent-file /workspace/{agent-file}
```

Environment variables are forwarded from the host shell (no values embedded in
the command — Docker inherits them when the flag has no `=value`).

### Open WebUI container

Image: `ghcr.io/open-webui/open-webui:main`  
No post-boot API calls. Pre-configure via env vars at `docker run` time:

```
OPENAI_API_BASE_URL=http://forge-agent-{id}:8080/v1
OPENAI_API_KEY=forge
```

`forge webui start` reads `agent.yaml` in cwd (same as `forge agent start`) to
derive the container name and port. Pass `--agent-file` to override.

Full invocation:

```
docker run -d
  --name open-webui
  --network forge-net
  -p {webui-port}:8080
  -v open-webui-data:/app/backend/data
  -e OPENAI_API_BASE_URL=http://forge-agent-{id}:8080/v1
  -e OPENAI_API_KEY=forge
  ghcr.io/open-webui/open-webui:main
```

Default webui host port: `3000`.

---

## Prereq checks

| Check             | `forge agent start` | `forge webui start` |
|-------------------|---------------------|---------------------|
| Docker CLI on PATH | ✓ required          | ✓ required          |
| Docker daemon running | ✓ required       | ✓ required          |
| Port available    | ✓ (from agent.yaml) | –                   |
| agent.yaml exists | ✓                   | ✓ (to get agent id) |

---

## File layout (new files only)

```
src/ForgeMission.Cli/
  Docker/
    DockerCli.cs            # Process.Start wrappers for docker CLI calls
    DockerPrereqChecker.cs  # shared prereq table (Spectre.Console)
    PrereqCheck.cs          # record(Label, Pass, Detail)
Dockerfile                  # repo root — builds forge:local image
```

CLI verbs added to `src/ForgeMission.Cli/Program.cs`:
- `BuildAgentCommand()` → `forge agent start/stop`
- `BuildWebuiCommand()` → `forge webui start/stop`

---

## Spokes

| Spoke | What it builds | File |
|-------|----------------|------|
| 1 | Spectre.Console + PrereqChecker | [spoke-1](phase-23-spoke-1-prereq-checker.md) |
| 2 | DockerCli helpers | [spoke-2](phase-23-spoke-2-docker-helpers.md) |
| 3 | `forge agent start/stop` | [spoke-3](phase-23-spoke-3-agent-commands.md) |
| 4 | `forge webui start/stop` | [spoke-4](phase-23-spoke-4-webui-commands.md) |

Spokes 1 and 2 have no dependencies on each other and can run in parallel.
Spoke 3 depends on 1 and 2. Spoke 4 depends on 1, 2, and 3 (container naming).
