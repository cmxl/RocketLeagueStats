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

// Bare-presence boolean flags — manual overrides
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

// Serilog — wire to host
builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .Enrich.FromLogContext());

// Fail-fast on hosted service exceptions
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);

builder.Services.AddRocketLeagueStatsCore(builder.Configuration);

builder.Services.AddHostedService<IniBootstrapHostedService>();
builder.Services.AddHostedService<StatsApiListenerService>();
builder.Services.AddHostedService<ConsoleRendererService>();
builder.Services.AddHostedService<JsonlEventLoggerService>();

await builder.Build().RunAsync();
