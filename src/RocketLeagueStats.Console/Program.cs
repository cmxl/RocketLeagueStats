using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketLeagueStats.Console.HostedServices;
using RocketLeagueStats.Core.DependencyInjection;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Valued CLI flags via switch mappings
builder.Configuration.AddCommandLine(args, switchMappings: new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["--port"] = "StatsApi:Port",
});

// Bare-presence boolean flags
if (args.Contains("--raw", StringComparer.Ordinal))
{
    builder.Configuration["Console:RawMode"] = "true";
}

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

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext());

builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);

builder.Services.AddRocketLeagueStatsCore(builder.Configuration);
builder.Services.AddRocketLeagueStatsHostingDefaults();
builder.Services.AddHostedService<ConsoleRendererService>();

await builder.Build().RunAsync();
