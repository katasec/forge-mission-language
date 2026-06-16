# Phase 23 — Spoke 1: PrereqChecker

**Status:** Planned  
**Hub:** [phase-23-container-commands.md](phase-23-container-commands.md)  
**Parallel with:** Spoke 2 (no dependency between them)

## Goal

Add Spectre.Console to the CLI project and implement a shared `DockerPrereqChecker`
that renders a status table and returns a pass/fail result. Both `forge agent start`
and `forge webui start` call this before taking any Docker action.

---

## AOT constraint (read before writing any code)

Use Spectre.Console **table and markup string APIs only**:
- `new Table()`, `.AddColumn()`, `.AddRow()` — safe
- `AnsiConsole.Write(table)` — safe
- `AnsiConsole.MarkupLine("[green]...[/]")` — safe

Do **not** use:
- `AnsiConsole.Prompt<T>` or any generic/reflection-based API
- `IRenderable` type dispatch
- `AnsiConsole.Live`, `AnsiConsole.Progress` (uses threads + reflection)

---

## Tasks

### 1. Add Spectre.Console to CLI project

File: `src/ForgeMission.Cli/ForgeMission.Cli.csproj`

Add:
```xml
<PackageReference Include="Spectre.Console" Version="0.49.1" />
```

Verify the build still produces a clean AOT binary after adding this package
(`make install` must succeed with 0 ILC errors).

---

### 2. Create `PrereqCheck.cs`

File: `src/ForgeMission.Cli/Docker/PrereqCheck.cs`

```csharp
namespace ForgeMission.Cli.Docker;

public enum PrereqStatus { Pass, Fail, Skipped }

public record PrereqCheck(string Label, PrereqStatus Status, string Detail);
```

---

### 3. Create `DockerPrereqChecker.cs`

File: `src/ForgeMission.Cli/Docker/DockerPrereqChecker.cs`

```csharp
namespace ForgeMission.Cli.Docker;

public static class DockerPrereqChecker
{
    public static PrereqCheck CheckDockerCli()
    public static PrereqCheck CheckDockerDaemon()
    public static PrereqCheck CheckPort(int port)
    public static PrereqCheck CheckFileExists(string path, string label)
    public static bool RunAndPrint(IEnumerable<PrereqCheck> checks)
}
```

#### `CheckDockerCli()`
- Run `docker --version` via `Process.Start`
- Pass: exit code 0; Detail = first line of stdout (e.g. `docker 27.3.1`)
- Fail: exit code non-zero or binary not found; Detail = "not found — install Docker Desktop"

#### `CheckDockerDaemon()`
- Run `docker info` via `Process.Start`, capture stderr
- Pass: exit code 0; Detail = "running"
- Fail: Detail = "not running — start Docker Desktop"

#### `CheckPort(int port)`
- Attempt `new TcpListener(IPAddress.Loopback, port)`, call `.Start()` then `.Stop()`
- Pass: no exception; Detail = $"port {port} available"
- Fail (SocketException): Detail = $"port {port} already in use"

#### `CheckFileExists(string path, string label)`
- `File.Exists(path)`
- Pass: Detail = path
- Fail: Detail = $"{path} not found"

#### `RunAndPrint(IEnumerable<PrereqCheck> checks)`

Renders a Spectre.Console table:

```
 Requirement        Status      Detail
 ───────────────── ─────────── ─────────────────────────────
 Docker CLI         ✓ pass      docker 27.3.1
 Docker daemon      ✓ pass      running
 Port 8080          ✓ pass      port 8080 available
 agent.yaml         ✓ pass      ./agent.yaml
```

- Green `✓ pass` for Pass
- Red `✗ fail` for Fail  
- Grey `– skip` for Skipped (a check that was not executed because a prior check failed)
- Returns `true` if all checks are Pass, `false` otherwise
- On failure, print: `[red]Prerequisites not met. Cannot continue.[/]`

**Skipping logic:** stop executing checks after the first Fail; remaining checks
are added to the list as `Skipped`.

---

### 4. Helper: run process and capture output

Both `CheckDockerCli` and `CheckDockerDaemon` need to shell out. Extract a small
private helper inside `DockerPrereqChecker` (or reuse `DockerCli` from Spoke 2 if
that spoke is merged first):

```csharp
private static (int ExitCode, string Stdout) RunProcess(string file, string args)
{
    var psi = new ProcessStartInfo(file, args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
    };
    try
    {
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout);
    }
    catch (Exception)
    {
        return (-1, string.Empty);
    }
}
```

---

### 5. AOT verification

After implementing:
```
make install
```
Must succeed with 0 ILC errors/warnings beyond the pre-existing YamlDotNet
suppressions already in `Cli.csproj`.

---

## Acceptance criteria

- `DockerPrereqChecker.RunAndPrint` renders a table with correct colour coding
- Stops executing checks after first failure (remaining show as Skipped)
- Returns `false` when any check fails
- `make install` still produces a clean AOT binary
