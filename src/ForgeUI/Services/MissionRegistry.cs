using ForgeMission.Cli;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeUI.Services;

public record MissionEntry(
    string                               Label,
    string                               Description,
    MclProgram                           Ast,
    Dictionary<string, ExpertDefinition> Experts,
    IExpertRunner                        Runner);

public class MissionRegistry
{
    public IReadOnlyList<MissionEntry> Missions { get; }
    public MissionEntry                Default  => Missions[0];

    public MissionRegistry(IReadOnlyList<MissionEntry> missions) =>
        Missions = missions;

    public static async Task<MissionRegistry> LoadAsync(
        IEnumerable<(string label, string description, string path)> specs,
        string apiKey)
    {
        var entries = new List<MissionEntry>();
        foreach (var (label, description, path) in specs)
        {
            var dir      = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var source   = await File.ReadAllTextAsync(path);
            var ast      = MclParser.Parse(source);
            var lockFile = LockFileIO.Read(Path.Combine(dir, "mcl.lock"));
            var experts  = ExpertResolver.ResolveAll(lockFile, dir, verbose: null, warnings: Console.Error);
            var manifest = ForgeTomlReader.TryRead(path);

            IExpertRunner runner;
            if (manifest?.Providers?.GetValueOrDefault("default") is { } profile)
            {
                // Inject the resolved API key so each mission uses the live env value.
                var effective = new ForgeMission.Core.Manifest.ProviderProfile
                {
                    Provider = profile.Provider,
                    Model    = profile.Model,
                    ApiKey   = apiKey,
                    Endpoint = profile.Endpoint,
                };
                runner = ProviderClientBuilder.Build(effective);
            }
            else
            {
                runner = new ForgeMission.Core.Adapters.ExecExpertRunner();
                Console.Error.WriteLine($"ForgeUI: no forge.toml for '{label}' — LLM steps won't work.");
            }

            entries.Add(new MissionEntry(label, description, ast, experts, runner));
        }
        return new MissionRegistry(entries);
    }
}
