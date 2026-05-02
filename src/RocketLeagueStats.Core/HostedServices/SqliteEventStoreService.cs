// FlushAsync is a forward-looking stub; real body (Tasks 12-15) will access instance state
// and consume both parameters. Suppress until then.
#pragma warning disable CA1822, IDE0052, IDE0060
namespace RocketLeagueStats.Core.HostedServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;

internal sealed class SqliteEventStoreService(
    StatsEventBus bus,
    IOptions<EventStoreOptions> options,
    EventStoreConnectionString connectionString,
    ILogger<SqliteEventStoreService> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDisabled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(SqliteEventStoreService)),
            "SQLite event store disabled.");

    private static readonly Action<ILogger, int, int, Exception?> LogStarted =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, nameof(SqliteEventStoreService)),
            "SQLite event store started — MaxBatchSize: {MaxBatchSize}, MaxBatchLatencyMs: {MaxBatchLatencyMs}");

    private static readonly Action<ILogger, int, Exception?> LogBatchFailed =
        LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(3, nameof(SqliteEventStoreService)),
            "Failed to flush event batch of size {BatchSize}; dropping batch.");

    private readonly EventStoreOptions options = options.Value;

    // Used in Tasks 12-15 when FlushAsync writes to SQLite.
    private readonly string connectionString = connectionString.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.Enabled)
        {
            LogDisabled(logger, null);
            return;
        }

        var reader = bus.Subscribe();
        LogStarted(logger, this.options.MaxBatchSize, this.options.MaxBatchLatencyMs, null);

        var maxLatency = TimeSpan.FromMilliseconds(this.options.MaxBatchLatencyMs);
        var buffer = new List<StatsEvent>(capacity: this.options.MaxBatchSize);
        var lastFlushAt = DateTime.UtcNow;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - lastFlushAt;
                var remaining = maxLatency - elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    remaining = TimeSpan.FromMilliseconds(1);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(remaining);

                try
                {
                    if (await reader.WaitToReadAsync(cts.Token))
                    {
                        while (buffer.Count < this.options.MaxBatchSize && reader.TryRead(out var evt))
                        {
                            buffer.Add(evt);
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // Latency window elapsed — fall through to flush check.
                }

                var shouldFlushBySize = buffer.Count >= this.options.MaxBatchSize;
                var shouldFlushByLatency = (DateTime.UtcNow - lastFlushAt) >= maxLatency && buffer.Count > 0;

                if (shouldFlushBySize || shouldFlushByLatency)
                {
                    try
                    {
                        await this.FlushAsync(buffer, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogBatchFailed(logger, buffer.Count, ex);
                    }
                    finally
                    {
                        buffer.Clear();
                        lastFlushAt = DateTime.UtcNow;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown; flush whatever's left.
            if (buffer.Count > 0)
            {
                try
                {
                    await this.FlushAsync(buffer, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    LogBatchFailed(logger, buffer.Count, ex);
                }
            }
        }
    }

    /// <summary>
    /// Flushes a batch to SQLite. Real implementation added in subsequent tasks (12-15).
    /// Empty stub keeps the loop alive while we TDD the writes.
    /// </summary>
    private Task FlushAsync(
        IReadOnlyList<StatsEvent> batch, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
