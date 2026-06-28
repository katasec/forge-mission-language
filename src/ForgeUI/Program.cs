using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Load the mission once at startup.
var missionPath = builder.Configuration["MissionPath"]
    ?? Path.Combine(
        Directory.GetCurrentDirectory(),
        "mission.mcl");

var missionDir = Path.GetDirectoryName(Path.GetFullPath(missionPath))!;
var source     = await File.ReadAllTextAsync(missionPath);
var ast        = MclParser.Parse(source);
var lockPath   = Path.Combine(missionDir, "mcl.lock");
var lockFile   = LockFileIO.Read(lockPath);
var expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, verbose: null, warnings: Console.Error);

builder.Services.AddSingleton(ast);
builder.Services.AddSingleton(expertDefs);

// Default runner — PipelineRunner overrides this per expert kind (exec/rule/http/onnx).
// For LLM experts, replace with DirectExpertRunner(chatClient).
builder.Services.AddSingleton<IExpertRunner>(new ExecExpertRunner());
builder.Services.AddScoped<MissionService>();

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
