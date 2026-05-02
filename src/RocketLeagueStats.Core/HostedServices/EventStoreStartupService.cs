namespace RocketLeagueStats.Core.HostedServices;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketLeagueStats.Core.Persistence;

internal sealed class EventStoreStartupService(
    EventStoreConnectionString connectionString,
    IServiceScopeFactory scopeFactory,
    ILogger<EventStoreStartupService> logger)
    : IHostedService
{
    private static readonly Action<ILogger, string, string, int, Exception?> LogReady =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(1, nameof(EventStoreStartupService)),
            "Event store ready — path: {Path}, size: {Size}, matches: {MatchCount}");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // DbContext is scoped; resolve it through a dedicated scope so the singleton hosted
        // service does not violate the captive-dependency rule.
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StatsDbContext>();

        // MigrateAsync opens its own internal SqliteConnection, applies pending migrations, and
        // closes. The SqliteEventStoreService later opens its own short-lived connections per batch
        // — the two never share a connection object. In WAL mode that's fine: SQLite handles
        // concurrent file access from multiple connections via the WAL log without locking conflicts.
        await dbContext.Database.MigrateAsync(cancellationToken);

        var path = StatsConnectionString.ExtractDataSourcePath(connectionString.Value);
        var size = FormatSize(File.Exists(path) ? new FileInfo(path).Length : 0L);
        var matchCount = await dbContext.Matches.CountAsync(cancellationToken);

        LogReady(logger, path, size, matchCount, null);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string FormatSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        return bytes switch
        {
            < (long)kb => $"{bytes} B",
            < (long)mb => $"{(bytes / kb).ToString("F1", CultureInfo.InvariantCulture)} KB",
            < (long)gb => $"{(bytes / mb).ToString("F1", CultureInfo.InvariantCulture)} MB",
            _ => $"{(bytes / gb).ToString("F2", CultureInfo.InvariantCulture)} GB",
        };
    }
}
