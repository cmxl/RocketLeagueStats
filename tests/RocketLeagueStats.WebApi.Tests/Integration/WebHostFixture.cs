namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketLeagueStats.Core.Bus;

public sealed class WebHostFixture : WebApplicationFactory<Program>
{
    private readonly string dbPath = Path.Combine(
        Path.GetTempPath(),
        $"rls-test-stats-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var settingsTemp = Path.Combine(Path.GetTempPath(), $"rls-test-settings-{Guid.NewGuid()}");
            Directory.CreateDirectory(settingsTemp);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Override settings dir so tests don't pollute %APPDATA%.
                ["Web:SettingsDirectory"] = settingsTemp,
                // Override stats DB path so tests don't read from / write to the user's real DB.
                ["ConnectionStrings:Stats"] = $"Data Source={this.dbPath}",
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        // Drop the SqliteConnection pool so the temp DB file isn't held open on Windows.
        SqliteConnection.ClearAllPools();
        TryDelete(this.dbPath);
        TryDelete(this.dbPath + "-wal");
        TryDelete(this.dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort: temp dir gets cleaned eventually.
        }
    }
}
