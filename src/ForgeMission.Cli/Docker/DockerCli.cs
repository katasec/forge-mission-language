using System.Diagnostics;

namespace ForgeMission.Cli.Docker;

public static class DockerCli
{
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

    public static async Task<bool> IsImagePresentAsync(string image)
    {
        var (exitCode, _, _) = await RunAsync($"image inspect {image}");
        return exitCode == 0;
    }

    public static async Task BuildImageAsync(string tag, string contextPath)
    {
        var psi = new ProcessStartInfo("docker", $"build -t {tag} {contextPath}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;

        var readOut = Task.Run(async () =>
        {
            string? line;
            while ((line = await p.StandardOutput.ReadLineAsync()) is not null)
                Console.WriteLine(line);
        });
        var readErr = Task.Run(async () =>
        {
            string? line;
            while ((line = await p.StandardError.ReadLineAsync()) is not null)
                Console.WriteLine(line);
        });

        await Task.WhenAll(readOut, readErr);
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"docker build failed (exit {p.ExitCode})");
    }

    public static async Task<bool> NetworkExistsAsync(string name)
    {
        var (exitCode, _, _) = await RunAsync($"network inspect {name}");
        return exitCode == 0;
    }

    public static async Task EnsureNetworkAsync(string name)
    {
        if (!await NetworkExistsAsync(name))
            await RunAsync($"network create {name}");
    }

    public static async Task<bool> IsContainerRunningAsync(string name)
    {
        var (_, stdout, _) = await RunAsync($"ps --filter name=^/{name}$ --format {{{{.Names}}}}");
        return stdout.Contains(name);
    }

    public static async Task<bool> ContainerExistsAsync(string name)
    {
        var (_, stdout, _) = await RunAsync($"ps -a --filter name=^/{name}$ --format {{{{.Names}}}}");
        return stdout.Contains(name);
    }

    public static async Task StartContainerAsync(string args)
    {
        var (exitCode, _, stderr) = await RunAsync($"run {args}");
        if (exitCode != 0)
            throw new InvalidOperationException($"docker run failed: {stderr}");
    }

    public static async Task StopAndRemoveAsync(string name)
    {
        await RunAsync($"stop {name}");
        await RunAsync($"rm {name}");
    }
}
