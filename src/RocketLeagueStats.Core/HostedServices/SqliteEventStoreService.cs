namespace RocketLeagueStats.Core.HostedServices;

using System.Text.Json;
using Microsoft.Data.Sqlite;
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

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly EventStoreOptions options = options.Value;

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

    private async Task FlushAsync(IReadOnlyList<StatsEvent> batch, CancellationToken cancellationToken)
    {
        // We open a fresh connection per batch instead of holding one for the lifetime of the service.
        // The connection-pool inside Microsoft.Data.Sqlite means this is cheap (the underlying SQLite
        // handle is reused), but it gives us automatic recovery: if a connection ever enters a bad
        // state we discard it on Dispose and the next batch gets a clean one. EF Core's DbContext
        // would normally manage this for us; raw ADO.NET pushes the responsibility back to us.
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        ApplyPragmas(connection);

        // BeginTransactionAsync returns DbTransaction; we cast to SqliteTransaction so the
        // commands' Transaction property gets the concrete type (Microsoft.Data.Sqlite's
        // SqliteCommand.Transaction is typed as SqliteTransaction, not DbTransaction).
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var insertEvent = connection.CreateCommand();
        insertEvent.Transaction = tx;
        // RETURNING Id requires SQLite >= 3.35 (bundled with Microsoft.Data.Sqlite 10). Saves a
        // round-trip vs the older `last_insert_rowid()` pattern; matters because we issue this
        // INSERT once per discrete event and need the Id for the subsequent participant rows.
        insertEvent.CommandText = """
            INSERT INTO Events (MatchGuid, EventName, TimestampUtc, Payload)
            VALUES ($matchGuid, $eventName, $ts, $payload)
            RETURNING Id;
            """;
        var pMatchGuid = insertEvent.Parameters.Add("$matchGuid", SqliteType.Text);
        var pEventName = insertEvent.Parameters.Add("$eventName", SqliteType.Text);
        var pTimestamp = insertEvent.Parameters.Add("$ts", SqliteType.Integer);
        var pPayload = insertEvent.Parameters.Add("$payload", SqliteType.Text);

        await using var insertParticipant = connection.CreateCommand();
        insertParticipant.Transaction = tx;
        insertParticipant.CommandText = """
            INSERT INTO EventParticipants (EventId, MatchGuid, PlayerName, Shortcut, TeamNum, Role, TimestampUtc)
            VALUES ($eventId, $matchGuid, $playerName, $shortcut, $teamNum, $role, $ts);
            """;
        var ppEventId = insertParticipant.Parameters.Add("$eventId", SqliteType.Integer);
        var ppMatchGuid = insertParticipant.Parameters.Add("$matchGuid", SqliteType.Text);
        var ppPlayerName = insertParticipant.Parameters.Add("$playerName", SqliteType.Text);
        var ppShortcut = insertParticipant.Parameters.Add("$shortcut", SqliteType.Integer);
        var ppTeamNum = insertParticipant.Parameters.Add("$teamNum", SqliteType.Integer);
        var ppRole = insertParticipant.Parameters.Add("$role", SqliteType.Text);
        var ppTimestamp = insertParticipant.Parameters.Add("$ts", SqliteType.Integer);

        foreach (var evt in batch)
        {
            var ts = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();

            pMatchGuid.Value = (object?)evt.MatchGuid ?? DBNull.Value;
            pEventName.Value = evt.EventName;
            pTimestamp.Value = ts;
            // evt.GetType() (not typeof(StatsEvent)) — System.Text.Json must see the concrete derived
            // type to emit the typed event's fields. Passing the base type would only serialize the
            // three envelope properties (EventName/Timestamp/MatchGuid). The reflection-based path is
            // intentional here: it covers UnknownDiscreteEvent and any future event types without
            // updating StatsEventJsonContext.
            pPayload.Value = JsonSerializer.Serialize(evt, evt.GetType(), PayloadJsonOptions);

            var eventId = (long)(await insertEvent.ExecuteScalarAsync(cancellationToken))!;

            if (evt.MatchGuid is null)
            {
                continue;
            }

            foreach (var p in EventParticipantExtractor.Extract(evt))
            {
                ppEventId.Value = eventId;
                ppMatchGuid.Value = evt.MatchGuid;
                ppPlayerName.Value = p.PlayerName;
                ppShortcut.Value = p.Shortcut;
                ppTeamNum.Value = p.TeamNum;
                ppRole.Value = p.Role;
                ppTimestamp.Value = ts;
                await insertParticipant.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // PRAGMAs must be set per connection: SQLite stores them in connection state, not the file.
        // Most importantly, foreign_keys=ON defaults to OFF on every new SQLite connection — EF Core's
        // SQLite provider sets it for its DbContext connections automatically, but our raw writer
        // pool does not. Without this we'd silently skip FK enforcement and let orphaned rows in.
        // journal_mode=WAL is persisted in the file so it only "takes" the first time and is a no-op
        // on subsequent connections; the redundant set is harmless and self-documenting.
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA cache_size = -8000;
            PRAGMA foreign_keys = ON;
            PRAGMA wal_autocheckpoint = 1000;
            """;
        cmd.ExecuteNonQuery();
    }
}
