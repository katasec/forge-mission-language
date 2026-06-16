# Phase 23 — Spoke 4: `forge webui start/stop`

**Status:** Planned  
**Hub:** [phase-23-container-commands.md](phase-23-container-commands.md)  
**Depends on:** Spoke 1 (PrereqChecker), Spoke 2 (DockerCli), Spoke 3 (container naming convention)

## Goal

Implement `forge webui start` and `forge webui stop`. Start an Open WebUI container
pre-configured to connect to the running forge agent via env vars — no post-boot
API calls, no manual Open WebUI setup.

---

## Background

Open WebUI supports OpenAI-compatible backends configured at startup via:
```
OPENAI_API_BASE_URL=http://{agent-container-name}:{port}/v1
OPENAI_API_KEY=forge
```

Both containers join `forge-net`, so Open WebUI resolves the agent by container name
(`forge-agent-{id}`). The host only needs to expose the Open WebUI port (default 3000).

Agent identity comes from `agent.yaml` (same file `forge agent start` reads) — so
`forge webui start` reads it from cwd by default. This means the typical workflow is:

```
cd agents/loop-demo-naive
forge agent start
forge webui start
```

---

## Tasks

### 1. Add `BuildWebuiCommand()` to `Program.cs`

File: `src/ForgeMission.Cli/Program.cs`

```csharp
var webuiCmd = new Command("webui", "Manage Open WebUI connected to a forge agent");
webuiCmd.AddCommand(BuildWebuiStartCommand());
webuiCmd.AddCommand(BuildWebuiStopCommand());
rootCommand.AddCommand(webuiCmd);
```

---

### 2. Implement `BuildWebuiStartCommand()`

Options:
- `--agent-file <path>` (default: `./agent.yaml`)
- `--port <int>` (default: `3000`) — host port for Open WebUI

Logic (in order):

```
a. Load agent.yaml (to get container name and agent port)

b. Run prereqs:
     CheckDockerCli()
     CheckDockerDaemon()
     CheckFileExists(agentFile, "agent.yaml")
   → if any fail, return exit code 1

c. Derive agent container URL (internal to forge-net):
     agentContainerName = $"forge-agent-{config.Id}"
     agentUrl = $"http://{agentContainerName}:{config.Port}/v1"

d. Check agent container is running:
     if (!await DockerCli.IsContainerRunningAsync(agentContainerName))
         AnsiConsole.MarkupLine($"[yellow]Agent container {agentContainerName} is not running.[/]")
         AnsiConsole.MarkupLine("[yellow]Run 'forge agent start' first.[/]")
         return exit code 1

e. await DockerCli.EnsureNetworkAsync("forge-net")

f. if (await DockerCli.ContainerExistsAsync("open-webui"))
       AnsiConsole.MarkupLine("[yellow]open-webui container already exists. Stop it first.[/]")
       return exit code 1

g. Build docker run args:
   -d
   --name open-webui
   --network forge-net
   -p {webuiPort}:8080
   -v open-webui-data:/app/backend/data
   -e OPENAI_API_BASE_URL={agentUrl}
   -e OPENAI_API_KEY=forge
   ghcr.io/open-webui/open-webui:main

h. AnsiConsole.MarkupLine("[grey]Pulling open-webui image (first run may take a minute)...[/]")
   await DockerCli.StartContainerAsync(args)

i. Print success:
   AnsiConsole.MarkupLine($"[green]✓[/] Open WebUI started")
   AnsiConsole.MarkupLine($"  URL      : [link]http://localhost:{webuiPort}[/]")
   AnsiConsole.MarkupLine($"  Agent    : {agentUrl}")
   AnsiConsole.MarkupLine($"  Container: open-webui")
```

---

### 3. Implement `BuildWebuiStopCommand()`

Options: none (Open WebUI container is always named `open-webui`)

Logic:
```
a. await DockerCli.StopAndRemoveAsync("open-webui")
b. AnsiConsole.MarkupLine("[green]✓[/] Open WebUI stopped")
```

---

### 4. AOT verification

```
make install
```
Must succeed with 0 new ILC errors/warnings.

---

## Expected CLI behaviour

```
$ forge webui start

 Checking prerequisites...

 Requirement        Status      Detail
 ───────────────── ─────────── ──────────────────────────
 Docker CLI         ✓ pass      docker 27.3.1
 Docker daemon      ✓ pass      running
 agent.yaml         ✓ pass      ./agent.yaml

Pulling open-webui image (first run may take a minute)...

✓ Open WebUI started
  URL      : http://localhost:3000
  Agent    : http://forge-agent-loop-demo-naive-v1:8080/v1
  Container: open-webui
```

Error case (agent not running):
```
$ forge webui start
[yellow] Agent container forge-agent-loop-demo-naive-v1 is not running.
         Run 'forge agent start' first.
```

---

## Full workflow (end to end)

```bash
cd agents/loop-demo-naive

forge agent start       # starts forge-agent-loop-demo-naive-v1 on :8080
forge webui start       # starts open-webui on :3000, points at agent

# Open http://localhost:3000 — Open WebUI is ready, agent pre-registered

forge webui stop        # tears down open-webui
forge agent stop        # tears down the agent
```

---

## Acceptance criteria

- `forge webui start` starts Open WebUI visible in `docker ps`
- Open WebUI at `http://localhost:3000` can send a chat request that reaches the forge agent
- `forge webui start` fails gracefully if agent container is not running
- `forge webui stop` removes the container cleanly
- `make install` produces a clean AOT binary
