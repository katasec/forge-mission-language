using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Core.Resolution;

public class ForgeCredentials
{
    public Dictionary<string, RegistryCredential> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class RegistryCredential
{
    public string Token { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ForgeCredentials))]
[JsonSerializable(typeof(RegistryCredential))]
internal partial class CredentialsJsonContext : JsonSerializerContext { }

public static class CredentialStore
{
    private static string CredentialsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".forge", "credentials.json");

    public static string? GetToken(string registry)
    {
        // Env var takes precedence for simple CI setups
        var envToken = Environment.GetEnvironmentVariable("FORGE_REGISTRY_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) return envToken;

        if (!File.Exists(CredentialsPath)) return null;

        try
        {
            var json = File.ReadAllText(CredentialsPath);
            var creds = JsonSerializer.Deserialize(json, CredentialsJsonContext.Default.ForgeCredentials);
            if (creds?.Credentials.TryGetValue(registry, out var cred) == true)
                return string.IsNullOrWhiteSpace(cred.Token) ? null : cred.Token;
        }
        catch { /* corrupt credentials file — treat as missing */ }

        return null;
    }

    public static void SaveToken(string registry, string token)
    {
        var dir = Path.GetDirectoryName(CredentialsPath)!;
        Directory.CreateDirectory(dir);

        ForgeCredentials existing;
        try
        {
            existing = File.Exists(CredentialsPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(CredentialsPath), CredentialsJsonContext.Default.ForgeCredentials) ?? new()
                : new();
        }
        catch { existing = new(); }

        existing.Credentials[registry] = new RegistryCredential { Token = token };
        File.WriteAllText(CredentialsPath,
            JsonSerializer.Serialize(existing, CredentialsJsonContext.Default.ForgeCredentials));
    }
}
