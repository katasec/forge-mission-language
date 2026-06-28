using ForgeMission.Core.Manifest;
using ForgeUI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Resolve API key from env (set MCL_API_KEY in your shell profile).
var missionDir = builder.Configuration["MissionDir"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "missions");
missionDir = Path.GetFullPath(missionDir);

// Read key from the first forge.toml that has one, or fall back to empty.
var apiKey = ForgeTomlReader.TryRead(Path.Combine(missionDir, "hallucination-guard", "mission.mcl"))
                 ?.Providers?.GetValueOrDefault("default")?.ApiKey
             ?? string.Empty;

var keyPrefix = apiKey is { Length: > 10 } ? apiKey[..10] + "..." : "(empty)";
Console.Error.WriteLine($"ForgeUI: API key length = {apiKey.Length}, prefix = {keyPrefix}");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ForgeUI: API key is empty — set MCL_API_KEY and restart.");
    return;
}

var registry = await MissionRegistry.LoadAsync(
[
    ("ChatGPT",  "Raw LLM — no verification",                    Path.Combine(missionDir, "vanilla",             "mission.mcl")),
    ("Forge",    "LLM + deterministic verifier, retries on fail", Path.Combine(missionDir, "hallucination-guard", "mission.mcl")),
],
apiKey);

builder.Services.AddSingleton(registry);
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<SessionStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
