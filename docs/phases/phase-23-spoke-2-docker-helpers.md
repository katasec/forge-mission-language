# Phase 23 — Spoke 2: Docker CLI Helpers

**Status:** Planned  
**Hub:** [phase-23-container-commands.md](phase-23-container-commands.md)  
**Parallel with:** Spoke 1 (no dependency between them)

## Goal

Implement `DockerCli` — a thin, AOT-safe wrapper around `Process.Start("docker", ...)`
calls. All Docker interactions in Spokes 3 and 4 go through this class. No Docker SDK,
no Newtonsoft.Json, no reflection.

---

## Tasks

### 1. Create `DockerCli.cs`

File: `src/ForgeMission.Cli/Docker/DockerCli.cs`

```csharp
namespace ForgeMission.Cli.Docker;

public static class DockerCli
{
    public static Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string args)
    public static Task<bool> IsImagePresentAsync(string image)
    public static Task BuildImageAsync(string tag, string contextPath)
    public static Task<bool> NetworkExistsAsync(string name)
    public static Task EnsureNetworkAsync(string name)
    public static Task<bool> IsContainerRunningAsync(string name)
    public static Task<bool> ContainerExistsAsync(string name)
    public static Task StartContainerAsync(string args)
    public static Task StopAndRemoveAsync(string name)
}
```

---

### 2. Implement `RunAsync`

Core primitive — everything else calls this:

```csharp
public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string args)
{
    var psi = new ProcessStartInfo("docker", args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
    };
    using var p = Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start docker process");

    var stdout = await p.StandardOutput.ReadToEndAsync();
    var stderr = await p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync();
    return (p.ExitCode, stdout.Trim(), stderr.Trim());
}
```

---

### 3. Implement each method

#### `IsImagePresentAsync(string image)`
```
docker image inspect {image}
```
Returns `true` if exit code 0.

#### `BuildImageAsync(string tag, string contextPath)`
```
docker build -t {tag} {contextPath}
```
Streams stdout to `Console.Out` so the user sees build progress.
Throws `InvalidOperationException` if exit code non-zero.

#### `NetworkExistsAsync(string name)`
```
docker network inspect {name}
```
Returns `true` if exit code 0.

#### `EnsureNetworkAsync(string name)`
```
if (!await NetworkExistsAsync(name))
    await RunAsync($"network create {name}")
```

#### `IsContainerRunningAsync(string name)`
```
docker ps --filter name=^/{name}$ --format {{.Names}}
```
Returns `true` if stdout contains `name`.

#### `ContainerExistsAsync(string name)`
```
docker ps -a --filter name=^/{name}$ --format {{.Names}}
```
Returns `true` if stdout contains `name` (running or stopped).

#### `StartContainerAsync(string args)`
```
docker run {args}
```
Throws `InvalidOperationException` with stderr if exit code non-zero.

#### `StopAndRemoveAsync(string name)`
Stop then remove, tolerating "not found" gracefully:
```
docker stop {name}    # ignore exit code
docker rm   {name}    # ignore exit code
```

---

### 4. BuildImageAsync: stream output

`docker build` can take 30–60 seconds on first run. Stream stdout line-by-line
so the user sees progress rather than a silent hang:

```csharp
public static async Task BuildImageAsync(string tag, string contextPath)
{
    var psi = new ProcessStartInfo("docker", $"build -t {tag} {contextPath}")
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
    };
    using var p = Process.Start(psi)!;

    // Stream both stdout and stderr (docker build mixes them)
    var readOut = Task.Run(async () => {
        string? line;
        while ((line = await p.StandardOutput.ReadLineAsync()) is not null)
            Console.WriteLine(line);
    });
    var readErr = Task.Run(async () => {
        string? line;
        while ((line = await p.StandardError.ReadLineAsync()) is not null)
            Console.WriteLine(line);
    });

    await Task.WhenAll(readOut, readErr);
    await p.WaitForExitAsync();

    if (p.ExitCode != 0)
        throw new InvalidOperationException($"docker build failed (exit {p.ExitCode})");
}
```

---

### 5. AOT verification

No new packages required for this spoke — `Process.Start` is in `System.Diagnostics`,
fully AOT-safe.

```
make install
```
Must succeed with 0 new ILC errors.

---

## Acceptance criteria

- All methods compile under AOT (`make install` clean)
- `RunAsync` correctly captures stdout, stderr, and exit code
- `EnsureNetworkAsync` is idempotent (safe to call multiple times)
- `StopAndRemoveAsync` does not throw if container does not exist
- `BuildImageAsync` streams output to console during build
