namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketLeagueStats.Core.Bus;

public sealed class WebHostFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // Override settings dir to a temp path so tests don't pollute %APPDATA%
            var temp = Path.Combine(Path.GetTempPath(), $"rls-test-settings-{Guid.NewGuid()}");
            Directory.CreateDirectory(temp);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Web:SettingsDirectory"] = temp,
            });
        });

        builder.ConfigureServices((ctx, services) =>
        {
            // Strip any hosted services that need real-world resources.
            // We keep LiveMatchProjector because tests verify the bus → hub broadcast.
            // Anything else (StatsApiListenerService, JsonlEventLoggerService,
            // IniBootstrapHostedService) gets removed.
            var toRemove = services
                .Where(s => s.ServiceType == typeof(IHostedService))
                .Where(s => s.ImplementationType?.Name is "StatsApiListenerService"
                                                       or "JsonlEventLoggerService"
                                                       or "IniBootstrapHostedService")
                .ToList();
            foreach (var d in toRemove)
            {
                services.Remove(d);
            }
        });
    }

    public StatsEventBus GetBus() => this.Services.GetRequiredService<StatsEventBus>();
}
