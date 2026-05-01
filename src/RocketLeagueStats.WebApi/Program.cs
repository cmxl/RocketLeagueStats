using RocketLeagueStats.Core.DependencyInjection;
using RocketLeagueStats.WebApi.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Valued CLI flags via switch mappings
builder.Configuration.AddCommandLine(args, switchMappings: new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["--port"] = "StatsApi:Port",
    ["--web-port"] = "Web:Port",
});

// Bare-presence boolean flags
if (args.Contains("--no-log", StringComparer.Ordinal))
{
    builder.Configuration["EventLog:Enabled"] = "false";
}

if (args.Contains("--no-config-helper", StringComparer.Ordinal))
{
    builder.Configuration["GameSetup:AutoConfigureIni"] = "false";
}

if (args.Contains("--trace", StringComparer.Ordinal))
{
    builder.Configuration["StatsApi:TraceMode"] = "true";
}

if (args.Contains("--dump-snapshot", StringComparer.Ordinal))
{
    builder.Configuration["Diagnostics:DumpSnapshots"] = "true";
}

// Serilog
builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext());

// Fail-fast on hosted service exceptions
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);

// Domain registrations
builder.Services.AddRocketLeagueStatsCore(builder.Configuration);
builder.Services.AddRocketLeagueStatsHostingDefaults();
builder.Services.AddRocketLeagueStatsWebApi(builder.Configuration);

// Bind Kestrel to 0.0.0.0:<web-port> (default 5000)
var webPort = int.TryParse(builder.Configuration["Web:Port"], out var p) ? p : 5000;
builder.WebHost.UseUrls($"http://0.0.0.0:{webPort}");

var app = builder.Build();

app.UseRocketLeagueStatsWebApi();

await app.RunAsync();
