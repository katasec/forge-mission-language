using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Spectre.Console;

namespace ForgeMission.Cli.Docker;

public static class DockerPrereqChecker
{
    public static PrereqCheck CheckDockerCli()
    {
        var (exitCode, stdout) = RunProcess("docker", "--version");
        if (exitCode == 0)
        {
            var detail = stdout.Split('\n')[0].Trim();
            return new PrereqCheck("Docker CLI", PrereqStatus.Pass, detail);
        }
        return new PrereqCheck("Docker CLI", PrereqStatus.Fail, "not found — install Docker Desktop");
    }

    public static PrereqCheck CheckDockerDaemon()
    {
        var (exitCode, _) = RunProcess("docker", "info");
        if (exitCode == 0)
            return new PrereqCheck("Docker daemon", PrereqStatus.Pass, "running");
        return new PrereqCheck("Docker daemon", PrereqStatus.Fail, "not running — start Docker Desktop");
    }

    public static PrereqCheck CheckPort(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return new PrereqCheck($"Port {port}", PrereqStatus.Pass, $"port {port} available");
        }
        catch (SocketException)
        {
            return new PrereqCheck($"Port {port}", PrereqStatus.Fail, $"port {port} already in use");
        }
    }

    public static PrereqCheck CheckFileExists(string path, string label)
    {
        if (File.Exists(path))
            return new PrereqCheck(label, PrereqStatus.Pass, path);
        return new PrereqCheck(label, PrereqStatus.Fail, $"{path} not found");
    }

    public static bool RunAndPrint(IEnumerable<PrereqCheck> checks)
    {
        AnsiConsole.MarkupLine("\n [bold]Checking prerequisites...[/]\n");

        var table = new Table();
        table.AddColumn("Requirement");
        table.AddColumn("Status");
        table.AddColumn("Detail");
        table.Border(TableBorder.Simple);

        var results = new List<PrereqCheck>();
        bool failed = false;

        foreach (var check in checks)
        {
            if (failed)
            {
                results.Add(check with { Status = PrereqStatus.Skipped, Detail = "–" });
            }
            else
            {
                results.Add(check);
                if (check.Status == PrereqStatus.Fail)
                    failed = true;
            }
        }

        foreach (var r in results)
        {
            var (statusMarkup, detailMarkup) = r.Status switch
            {
                PrereqStatus.Pass    => ("[green]✓ pass[/]", r.Detail),
                PrereqStatus.Fail    => ("[red]✗ fail[/]", r.Detail),
                PrereqStatus.Skipped => ("[grey]– skip[/]", "[grey]–[/]"),
                _                    => ("?", r.Detail)
            };
            table.AddRow(r.Label, statusMarkup, detailMarkup);
        }

        AnsiConsole.Write(table);

        if (failed)
            AnsiConsole.MarkupLine("[red]Prerequisites not met. Cannot continue.[/]");

        return !failed;
    }

    internal static (int ExitCode, string Stdout) RunProcess(string file, string args)
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
}
