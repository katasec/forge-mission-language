# Phase 23 — Spoke 3: `forge agent start/stop`

**Status:** Planned  
**Hub:** [phase-23-container-commands.md](phase-23-container-commands.md)  
**Depends on:** Spoke 1 (PrereqChecker), Spoke 2 (DockerCli)

## Goal

Implement `forge agent start` and `forge agent stop`. These containerise `forge serve`
so the agent runs in Docker rather than directly on the host.

---

## Background

`forge serve` (Phase 19) reads `agent.yaml` and starts an OAI-compatible HTTP server.
`forge agent start` does the same thing but inside a `forge:local` Docker container,
with the current directory mounted as `/workspace`.

`agent.yaml` fields used:
- `mission` — path to `.mcl` file (relative to workspace)
- `port` — host port to expose
- `id` — used to name the container (`forge-agent-{id}`)

---

## Tasks

### 1. Create `Dockerfile` at repo root

File: `Dockerfile`

```dockerfile
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y libssl3 ca-certificates && rm -rf /var/lib/apt/lists/*
COPY forge-linux-x64 /usr/local/bin/forge
RUN chmod +x /usr/local/bin/forge
ENTRYPOINT ["forge"]
```

Notes:
- `libssl3` and `ca-certificates` are required by the AOT binary for HTTPS calls to OpenAI
- `forge-linux-x64` must be present alongside the Dockerfile at build time
- Locally: run `make build-linux` first (see Task 2)
- In CI: the release workflow already publishes `forge-linux-x64`

---

### 2. Add `build-linux` target to Makefile

File: `Makefile`

Add:
```makefile
build-linux: ## Build linux-x64 binary into repo root (needed for docker build)
	dotnet publish $(CLI) -c Release -r linux-x64 -o . --self-contained
	@echo "forge-linux-x64 ready"
```

This publishes the Linux AOT binary to the repo root so `docker build` can COPY it.

---

### 3. Add `BuildAgentCommand()` to `Program.cs`

File: `src/ForgeMission.Cli/Program.cs`

Add a subcommand group:

```csharp
var agentCmd = new Command("agent", "Manage forge agents running in Docker");
agentCmd.AddCommand(BuildAgentStartCommand());
agentCmd.AddCommand(BuildAgentStopCommand());
rootCommand.AddCommand(agentCmd);
```

---

### 4. Implement `BuildAgentStartCommand()`

Options:
- `--agent-file <path>` (default: `./agent.yaml`)

Logic (in order):

```
a. Load agent.yaml (AgentConfigLoader.Load)
b. Run prereqs:
     CheckDockerCli()
     CheckDockerDaemon()
     CheckPort(config.Port)
     CheckFileExists(agentFile, "agent.yaml")
   → if any fail, return exit code 1

c. Ensure forge:local image exists
     if (!await DockerCli.IsImagePresentAsync("forge:local"))
         AnsiConsole.MarkupLine("[yellow]Building forge:local image...[/]")
         await DockerCli.BuildImageAsync("forge:local", ".")

d. await DockerCli.EnsureNetworkAsync("forge-net")

e. Container name = $"forge-agent-{config.Id}"
   if (await DockerCli.ContainerExistsAsync(containerName))
       AnsiConsole.MarkupLine($"[yellow]Container {containerName} already exists. Stop it first.[/]")
       return exit code 1

f. Build docker run args:
   -d
   --name {containerName}
   --network forge-net
   -p {config.Port}:{config.Port}
   -v {Directory.GetCurrentDirectory()}:/workspace
   -e MCL_API_KEY
   -e MCL_MODEL
   -e MCL_PROVIDER
   -e MCL_ENDPOINT
   forge:local
   serve --agent-file /workspace/{agentFile}

g. await DockerCli.StartContainerAsync(args)

h. Print success:
   AnsiConsole.MarkupLine($"[green]✓[/] Agent [bold]{config.Id}[/] started")
   AnsiConsole.MarkupLine($"  Endpoint : [link]http://localhost:{config.Port}/v1[/]")
   AnsiConsole.MarkupLine($"  Container: {containerName}")
   AnsiConsole.MarkupLine($"  Network  : forge-net")
```

**Env var forwarding:** Pass `-e MCL_API_KEY` (no `=value`) — Docker inherits
the value from the host shell. Only forward variables that are set on the host;
skip unset ones to avoid Docker errors.

Helper to build env args:
```csharp
private static string EnvArgs(params string[] vars) =>
    string.Join(" ", vars
        .Where(v => Environment.GetEnvironmentVariable(v) is not null)
        .Select(v => $"-e {v}"));
```

---

### 5. Implement `BuildAgentStopCommand()`

Options:
- `--agent-file <path>` (default: `./agent.yaml`)

Logic:
```
a. Load agent.yaml
b. Container name = $"forge-agent-{config.Id}"
c. await DockerCli.StopAndRemoveAsync(containerName)
d. AnsiConsole.MarkupLine($"[green]✓[/] Agent [bold]{config.Id}[/] stopped")
```

---

### 6. AOT verification

```
make install
```
Must succeed with 0 new ILC errors/warnings.

---

## Expected CLI behaviour

```
$ cd agents/loop-demo-naive
$ forge agent start

 Checking prerequisites...

 Requirement        Status      Detail
 ───────────────── ─────────── ──────────────────────────
 Docker CLI         ✓ pass      docker 27.3.1
 Docker daemon      ✓ pass      running
 Port 8080          ✓ pass      port 8080 available
 agent.yaml         ✓ pass      ./agent.yaml

Building forge:local image...
[docker build output streams here]

✓ Agent loop-demo-naive-v1 started
  Endpoint : http://localhost:8080/v1
  Container: forge-agent-loop-demo-naive-v1
  Network  : forge-net
```

---

## Acceptance criteria

- `forge agent start` starts a container visible in `docker ps`
- Agent endpoint responds to `curl http://localhost:{port}/v1/chat/completions`
- `forge agent stop` removes the container cleanly
- `make install` produces a clean AOT binary
