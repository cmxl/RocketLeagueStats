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

    // Carries the most-recent MatchStateSnapshotData per active match across batches. We need it
    // at MatchEnded time to populate the Match row's team metadata + arena + the PlayerMatchStats
    // rows — Rocket League's wire never re-sends those once the match is over, so we have to
    // capture them while the match is in progress. Bounded by the number of concurrently-active
    // matches (effectively 1 in single-client usage); cleared per match when MatchEnded fires.
    private readonly Dictionary<string, MatchStateSnapshotData> latestSnapshotByMatch =
        new(StringComparer.Ordinal);

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
        var hasTerminalEvent = false;

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
                            // Drop training / free-play / private-match events: the wire emits an
                            // empty MatchGuid for those modes, and we deliberately don't persist them
                            // (no recap, no history, no analytics). Filtering at the bus-read step
                            // keeps the Events / MatchSnapshots / EventParticipants tables clean of
                            // ghost rows that will never have a parent Match.
                            if (string.IsNullOrEmpty(evt.MatchGuid))
                            {
                                continue;
                            }

                            buffer.Add(evt);

                            // MatchEnded / MatchDestroyed signal a match concluding. Force a flush
                            // so the recap row is persisted before the user's "show recap" click
                            // hits the read endpoint. Without this, the next flush is up to
                            // MaxBatchLatencyMs (250ms default) away and a fast click 404s. The
                            // live projector emits OnMatchEnded over SignalR on the same bus tick,
                            // so the user can act on the toast immediately — the writer has to
                            // catch up just as fast.
                            if (evt is MatchEndedEvent or MatchDestroyedEvent)
                            {
                                hasTerminalEvent = true;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // Latency window elapsed — fall through to flush check.
                }

                var shouldFlushBySize = buffer.Count >= this.options.MaxBatchSize;
                var shouldFlushByLatency = (DateTime.UtcNow - lastFlushAt) >= maxLatency && buffer.Count > 0;
                var shouldFlushByTerminal = hasTerminalEvent && buffer.Count > 0;

                if (shouldFlushBySize || shouldFlushByLatency || shouldFlushByTerminal)
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
                        hasTerminalEvent = false;
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

        await using var insertSnapshot = connection.CreateCommand();
        insertSnapshot.Transaction = tx;
        insertSnapshot.CommandText = """
            INSERT INTO MatchSnapshots (MatchGuid, TimestampUtc, Payload)
            VALUES ($matchGuid, $ts, $payload);
            """;
        var spMatchGuid = insertSnapshot.Parameters.Add("$matchGuid", SqliteType.Text);
        var spTimestamp = insertSnapshot.Parameters.Add("$ts", SqliteType.Integer);
        var spPayload = insertSnapshot.Parameters.Add("$payload", SqliteType.Text);

        await using var upsertMatch = connection.CreateCommand();
        upsertMatch.Transaction = tx;
        // SQLite UPSERT (ON CONFLICT … DO UPDATE) — `excluded.<col>` is the would-be-inserted value
        // for that column on a conflict. EF Core has no first-class UPSERT API, so we do this in raw
        // SQL rather than load + change-track + save (which would issue SELECT + INSERT/UPDATE for
        // every match in every batch).
        // - EventCount/SnapshotCount: ADD the per-batch delta to the existing total.
        // - LastEventAtUtc: take the larger of existing vs new — events within a batch may not be
        //   in arrival order if multiple subscribers race.
        // - Lifecycle timestamps + WinnerTeamNum: COALESCE keeps the FIRST non-null value seen.
        //   This means if MatchEnded fires twice (which it shouldn't), the original WinnerTeamNum wins.
        // - Team metadata + arena: COALESCE-preserved too, but only sent at MatchEnded — every
        //   in-progress upsert passes NULL so the existing values aren't disturbed mid-match.
        upsertMatch.CommandText = """
            INSERT INTO Matches (MatchGuid, FirstSeenAtUtc, EventCount, SnapshotCount, LastEventAtUtc,
                                  CreatedAtUtc, InitializedAtUtc, EndedAtUtc, DestroyedAtUtc, WinnerTeamNum,
                                  BlueTeamName, BlueColorPrimary, BlueColorSecondary,
                                  OrangeTeamName, OrangeColorPrimary, OrangeColorSecondary, Arena)
            VALUES ($matchGuid, $firstSeen, $eventDelta, $snapshotDelta, $lastTs,
                    $created, $initialized, $ended, $destroyed, $winner,
                    $blueName, $bluePrimary, $blueSecondary,
                    $orangeName, $orangePrimary, $orangeSecondary, $arena)
            ON CONFLICT(MatchGuid) DO UPDATE SET
                EventCount     = EventCount + excluded.EventCount,
                SnapshotCount  = SnapshotCount + excluded.SnapshotCount,
                LastEventAtUtc = MAX(LastEventAtUtc, excluded.LastEventAtUtc),
                CreatedAtUtc        = COALESCE(CreatedAtUtc,        excluded.CreatedAtUtc),
                InitializedAtUtc    = COALESCE(InitializedAtUtc,    excluded.InitializedAtUtc),
                EndedAtUtc          = COALESCE(EndedAtUtc,          excluded.EndedAtUtc),
                DestroyedAtUtc      = COALESCE(DestroyedAtUtc,      excluded.DestroyedAtUtc),
                WinnerTeamNum       = COALESCE(WinnerTeamNum,       excluded.WinnerTeamNum),
                BlueTeamName         = COALESCE(BlueTeamName,         excluded.BlueTeamName),
                BlueColorPrimary     = COALESCE(BlueColorPrimary,     excluded.BlueColorPrimary),
                BlueColorSecondary   = COALESCE(BlueColorSecondary,   excluded.BlueColorSecondary),
                OrangeTeamName       = COALESCE(OrangeTeamName,       excluded.OrangeTeamName),
                OrangeColorPrimary   = COALESCE(OrangeColorPrimary,   excluded.OrangeColorPrimary),
                OrangeColorSecondary = COALESCE(OrangeColorSecondary, excluded.OrangeColorSecondary),
                Arena                = COALESCE(Arena,                excluded.Arena);
            """;
        var umMatchGuid = upsertMatch.Parameters.Add("$matchGuid", SqliteType.Text);
        var umFirstSeen = upsertMatch.Parameters.Add("$firstSeen", SqliteType.Integer);
        var umEventDelta = upsertMatch.Parameters.Add("$eventDelta", SqliteType.Integer);
        var umSnapshotDelta = upsertMatch.Parameters.Add("$snapshotDelta", SqliteType.Integer);
        var umLastTs = upsertMatch.Parameters.Add("$lastTs", SqliteType.Integer);
        var umCreated = upsertMatch.Parameters.Add("$created", SqliteType.Integer);
        var umInitialized = upsertMatch.Parameters.Add("$initialized", SqliteType.Integer);
        var umEnded = upsertMatch.Parameters.Add("$ended", SqliteType.Integer);
        var umDestroyed = upsertMatch.Parameters.Add("$destroyed", SqliteType.Integer);
        var umWinner = upsertMatch.Parameters.Add("$winner", SqliteType.Integer);
        var umBlueName = upsertMatch.Parameters.Add("$blueName", SqliteType.Text);
        var umBluePrimary = upsertMatch.Parameters.Add("$bluePrimary", SqliteType.Text);
        var umBlueSecondary = upsertMatch.Parameters.Add("$blueSecondary", SqliteType.Text);
        var umOrangeName = upsertMatch.Parameters.Add("$orangeName", SqliteType.Text);
        var umOrangePrimary = upsertMatch.Parameters.Add("$orangePrimary", SqliteType.Text);
        var umOrangeSecondary = upsertMatch.Parameters.Add("$orangeSecondary", SqliteType.Text);
        var umArena = upsertMatch.Parameters.Add("$arena", SqliteType.Text);

        await using var upsertPlayerStats = connection.CreateCommand();
        upsertPlayerStats.Transaction = tx;
        // INSERT … ON CONFLICT(MatchGuid, Shortcut) DO UPDATE — defensive idempotency in case
        // MatchEnded fires twice for the same match (shouldn't happen, but the wire occasionally
        // double-fires lifecycle events). Subsequent calls overwrite with the most recent
        // snapshot's values, which is what we want — final scoreboard wins.
        upsertPlayerStats.CommandText = """
            INSERT INTO PlayerMatchStats
                (MatchGuid, Shortcut, PlayerName, TeamNum, Platform, Score, Goals, Assists, Saves, Shots, Touches)
            VALUES
                ($matchGuid, $shortcut, $playerName, $teamNum, $platform, $score, $goals, $assists, $saves, $shots, $touches)
            ON CONFLICT(MatchGuid, Shortcut) DO UPDATE SET
                PlayerName = excluded.PlayerName,
                TeamNum    = excluded.TeamNum,
                Platform   = excluded.Platform,
                Score      = excluded.Score,
                Goals      = excluded.Goals,
                Assists    = excluded.Assists,
                Saves      = excluded.Saves,
                Shots      = excluded.Shots,
                Touches    = excluded.Touches;
            """;
        var psMatchGuid = upsertPlayerStats.Parameters.Add("$matchGuid", SqliteType.Text);
        var psShortcut = upsertPlayerStats.Parameters.Add("$shortcut", SqliteType.Integer);
        var psPlayerName = upsertPlayerStats.Parameters.Add("$playerName", SqliteType.Text);
        var psTeamNum = upsertPlayerStats.Parameters.Add("$teamNum", SqliteType.Integer);
        var psPlatform = upsertPlayerStats.Parameters.Add("$platform", SqliteType.Text);
        var psScore = upsertPlayerStats.Parameters.Add("$score", SqliteType.Integer);
        var psGoals = upsertPlayerStats.Parameters.Add("$goals", SqliteType.Integer);
        var psAssists = upsertPlayerStats.Parameters.Add("$assists", SqliteType.Integer);
        var psSaves = upsertPlayerStats.Parameters.Add("$saves", SqliteType.Integer);
        var psShots = upsertPlayerStats.Parameters.Add("$shots", SqliteType.Integer);
        var psTouches = upsertPlayerStats.Parameters.Add("$touches", SqliteType.Integer);

        var matchAggregates = new Dictionary<string, MatchAggregate>(StringComparer.Ordinal);
        var matchesEndingInThisBatch = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in batch)
        {
            if (evt.MatchGuid is null)
            {
                continue;
            }

            var ts = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
            if (!matchAggregates.TryGetValue(evt.MatchGuid, out var agg))
            {
                agg = new MatchAggregate(ts);
            }

            if (evt is MatchStateSnapshot snap)
            {
                agg = agg with { SnapshotDelta = agg.SnapshotDelta + 1, LastTs = Math.Max(agg.LastTs, ts) };

                // Capture the parsed snapshot data so we can pull team metadata + per-player
                // wire stats out of it when the match ends. Multiple snapshots within a single
                // batch will overwrite — last-one-wins is what we want, since we just want the
                // most recent state at MatchEnded time.
                if (MatchStateSnapshotData.TryParse(snap.RawData, out var snapData) && snapData is not null)
                {
                    this.latestSnapshotByMatch[evt.MatchGuid] = snapData;
                }
            }
            else
            {
                agg = agg with { EventDelta = agg.EventDelta + 1, LastTs = Math.Max(agg.LastTs, ts) };
            }

            agg = evt switch
            {
                MatchCreatedEvent => agg with { Created = agg.Created ?? ts },
                MatchInitializedEvent => agg with { Initialized = agg.Initialized ?? ts },
                MatchEndedEvent end => agg with { Ended = agg.Ended ?? ts, Winner = agg.Winner ?? end.WinnerTeamNum },
                MatchDestroyedEvent => agg with { Destroyed = agg.Destroyed ?? ts },
                _ => agg,
            };

            // Treat both MatchEnded (ranked) and MatchDestroyed (training/free-play, or ranked
            // post-podium) as match-end signals — we want to persist team metadata + per-player
            // stats whenever the match concludes by either path.
            if (evt is MatchEndedEvent or MatchDestroyedEvent)
            {
                matchesEndingInThisBatch.Add(evt.MatchGuid);
            }

            matchAggregates[evt.MatchGuid] = agg;
        }

        // Upsert all Matches BEFORE the per-event/snapshot inserts below — Events.MatchGuid and
        // MatchSnapshots.MatchGuid are FKs to Matches.MatchGuid with foreign_keys=ON. Without this
        // ordering, the first event of a brand-new match would fail with FOREIGN KEY constraint failed.
        foreach (var (guid, agg) in matchAggregates)
        {
            umMatchGuid.Value = guid;
            umFirstSeen.Value = agg.FirstSeen;
            umEventDelta.Value = agg.EventDelta;
            umSnapshotDelta.Value = agg.SnapshotDelta;
            umLastTs.Value = agg.LastTs;
            umCreated.Value = (object?)agg.Created ?? DBNull.Value;
            umInitialized.Value = (object?)agg.Initialized ?? DBNull.Value;
            umEnded.Value = (object?)agg.Ended ?? DBNull.Value;
            umDestroyed.Value = (object?)agg.Destroyed ?? DBNull.Value;
            umWinner.Value = (object?)agg.Winner ?? DBNull.Value;

            // Team metadata + arena are only persisted at match-end. For mid-match upserts we
            // pass NULL for all seven; the COALESCE in the ON CONFLICT clause keeps the existing
            // values (which are also NULL the first time, real values at MatchEnded time).
            var carryTeamMetadata =
                matchesEndingInThisBatch.Contains(guid)
                && this.latestSnapshotByMatch.TryGetValue(guid, out var snapForMatch);
            if (carryTeamMetadata)
            {
                var snap = this.latestSnapshotByMatch[guid];
                var blueTeam = FindTeam(snap.Teams, teamNum: 0);
                var orangeTeam = FindTeam(snap.Teams, teamNum: 1);
                umBlueName.Value = (object?)blueTeam?.Name ?? DBNull.Value;
                umBluePrimary.Value = (object?)blueTeam?.ColorPrimary ?? DBNull.Value;
                umBlueSecondary.Value = (object?)blueTeam?.ColorSecondary ?? DBNull.Value;
                umOrangeName.Value = (object?)orangeTeam?.Name ?? DBNull.Value;
                umOrangePrimary.Value = (object?)orangeTeam?.ColorPrimary ?? DBNull.Value;
                umOrangeSecondary.Value = (object?)orangeTeam?.ColorSecondary ?? DBNull.Value;
                umArena.Value = (object?)snap.Arena ?? DBNull.Value;
            }
            else
            {
                umBlueName.Value = DBNull.Value;
                umBluePrimary.Value = DBNull.Value;
                umBlueSecondary.Value = DBNull.Value;
                umOrangeName.Value = DBNull.Value;
                umOrangePrimary.Value = DBNull.Value;
                umOrangeSecondary.Value = DBNull.Value;
                umArena.Value = DBNull.Value;
            }

            await upsertMatch.ExecuteNonQueryAsync(cancellationToken);
        }

        // After Matches rows are upserted, persist PlayerMatchStats for any match that ended in
        // this batch. PlayerMatchStats.MatchGuid is FK to Matches — that's why we wait until
        // after the Matches upsert above. Each player gets one row per match (PK is
        // MatchGuid+Shortcut); the upsert overwrites if a duplicate MatchEnded fires.
        foreach (var endedGuid in matchesEndingInThisBatch)
        {
            if (!this.latestSnapshotByMatch.TryGetValue(endedGuid, out var snap))
            {
                continue;
            }

            foreach (var player in snap.Players)
            {
                psMatchGuid.Value = endedGuid;
                psShortcut.Value = player.Shortcut;
                psPlayerName.Value = player.Name;
                psTeamNum.Value = player.TeamNum;
                psPlatform.Value = player.Platform;
                psScore.Value = player.Score;
                psGoals.Value = player.Goals;
                psAssists.Value = player.Assists;
                psSaves.Value = player.Saves;
                psShots.Value = player.Shots;
                psTouches.Value = player.Touches;
                await upsertPlayerStats.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        foreach (var evt in batch)
        {
            var ts = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();

            if (evt is MatchStateSnapshot snap)
            {
                if (snap.MatchGuid is null)
                {
                    continue;   // snapshots without a match guid have no home
                }

                spMatchGuid.Value = snap.MatchGuid;
                spTimestamp.Value = ts;
                spPayload.Value = snap.RawData.GetRawText();
                await insertSnapshot.ExecuteNonQueryAsync(cancellationToken);
                continue;
            }

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

        // After a successful commit, drop the in-memory snapshot data for matches that ended.
        // Without this, the dictionary would keep growing as new matches start without ending
        // (e.g. process restart mid-match). We deliberately keep entries for in-progress matches
        // — there might be more snapshots before MatchEnded for those.
        foreach (var endedGuid in matchesEndingInThisBatch)
        {
            this.latestSnapshotByMatch.Remove(endedGuid);
        }
    }

    private static SnapshotTeam? FindTeam(IReadOnlyList<SnapshotTeam> teams, int teamNum)
    {
        for (var i = 0; i < teams.Count; i++)
        {
            if (teams[i].TeamNum == teamNum)
            {
                return teams[i];
            }
        }

        return null;
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

    private readonly record struct MatchAggregate(
        long FirstSeen,
        long LastTs = 0,
        long EventDelta = 0,
        long SnapshotDelta = 0,
        long? Created = null,
        long? Initialized = null,
        long? Ended = null,
        long? Destroyed = null,
        int? Winner = null);
}
