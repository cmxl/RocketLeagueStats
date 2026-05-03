namespace RocketLeagueStats.Core.HostedServices;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.Core.Persistence.Entities;

/// <summary>
/// One-shot startup task that fills team metadata + per-player wire stats on historical Match
/// rows whose data was lost because the writer hadn't shipped the projection yet (the
/// AddTeamMetadataAndPlayerStats migration runs on schema upgrade, but matches played by an
/// older binary leave Arena/BlueTeamName/... NULL and PlayerMatchStats empty). We rebuild the
/// missing data from the most recent <c>MatchSnapshots</c> row per match, which carries the
/// final scoreboard verbatim.
/// </summary>
/// <remarks>
/// Idempotent: only fills NULL columns and only inserts player rows for matches that have none.
/// Re-running on an already-backfilled DB is a no-op pass over the index. Safe to register
/// after every startup; the cost is bounded by the number of matches that match the
/// "needs backfill" predicate (typically zero on a fresh DB).
/// </remarks>
internal sealed class HistoricalDataBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<HistoricalDataBackfillService> logger)
    : IHostedService
{
    private static readonly Action<ILogger, int, Exception?> LogStarting =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(HistoricalDataBackfillService)),
            "Backfilling team metadata + PlayerMatchStats for {MatchCount} historical matches.");

    private static readonly Action<ILogger, int, int, Exception?> LogFinished =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, nameof(HistoricalDataBackfillService)),
            "Backfill complete — {Filled} matches updated, {Skipped} skipped (missing or malformed snapshot).");

    private static readonly Action<ILogger, string, Exception?> LogSkippedMalformedSnapshot =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(HistoricalDataBackfillService)),
            "Skipping match {MatchGuid} — latest snapshot payload could not be parsed.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Same captive-dependency pattern as EventStoreStartupService — singleton hosted service
        // resolves a scoped DbContext through a per-call scope so EF's change tracker stays
        // request-bounded.
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StatsDbContext>();

        var candidates = await FindMatchesNeedingBackfillAsync(db, cancellationToken);
        if (candidates.Count == 0)
        {
            return;
        }

        LogStarting(logger, candidates.Count, null);

        var matchesWithStats = await db.PlayerMatchStats
            .Select(p => p.MatchGuid)
            .Distinct()
            .ToListAsync(cancellationToken);
        var matchesWithStatsSet = new HashSet<string>(matchesWithStats, StringComparer.Ordinal);

        var filled = 0;
        var skipped = 0;

        foreach (var matchGuid in candidates)
        {
            // Latest snapshot is the post-MatchEnded scoreboard — RL keeps streaming UpdateState
            // ticks during the podium phase, so the highest-Id snapshot has the final tallies.
            var latestSnapshotPayload = await db.MatchSnapshots
                .AsNoTracking()
                .Where(s => s.MatchGuid == matchGuid)
                .OrderByDescending(s => s.Id)
                .Select(s => s.Payload)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSnapshotPayload is null)
            {
                skipped++;
                continue;
            }

            MatchStateSnapshotData? snapshotData;
            try
            {
                using var doc = JsonDocument.Parse(latestSnapshotPayload);
                if (!MatchStateSnapshotData.TryParse(doc.RootElement, out snapshotData) || snapshotData is null)
                {
                    LogSkippedMalformedSnapshot(logger, matchGuid, null);
                    skipped++;
                    continue;
                }
            }
            catch (JsonException ex)
            {
                LogSkippedMalformedSnapshot(logger, matchGuid, ex);
                skipped++;
                continue;
            }

            var match = await db.Matches.FirstAsync(m => m.MatchGuid == matchGuid, cancellationToken);
            FillMatchMetadata(match, snapshotData);

            if (!matchesWithStatsSet.Contains(matchGuid))
            {
                AddPlayerStats(db, matchGuid, snapshotData);
            }

            filled++;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogFinished(logger, filled, skipped, null);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<List<string>> FindMatchesNeedingBackfillAsync(StatsDbContext db, CancellationToken ct)
    {
        // Two predicates joined into one set:
        // 1. Ended match with NULL Arena → metadata never landed (live writer didn't run on this match).
        // 2. Ended match with no PlayerMatchStats rows → per-player projection didn't run either.
        // Either alone qualifies for backfill; we re-check inside the loop whether each piece needs work.
        var endedMatches = db.Matches.Where(m => m.EndedAtUtc != null);

        var missingArena = await endedMatches
            .Where(m => m.Arena == null)
            .Select(m => m.MatchGuid)
            .ToListAsync(ct);

        var missingPlayerStats = await endedMatches
            .Where(m => !db.PlayerMatchStats.Any(p => p.MatchGuid == m.MatchGuid))
            .Select(m => m.MatchGuid)
            .ToListAsync(ct);

        return [.. missingArena.Union(missingPlayerStats, StringComparer.Ordinal)];
    }

    private static void FillMatchMetadata(Match match, MatchStateSnapshotData snapshot)
    {
        // Null-coalesce per column so a partial-fill row (e.g. arena set, colors NULL) gets the
        // missing pieces from the snapshot without clobbering anything already stored.
        match.Arena ??= snapshot.Arena;

        var blue = FindTeam(snapshot.Teams, teamNum: 0);
        if (blue is not null)
        {
            match.BlueTeamName ??= blue.Name;
            match.BlueColorPrimary ??= blue.ColorPrimary;
            match.BlueColorSecondary ??= blue.ColorSecondary;
        }

        var orange = FindTeam(snapshot.Teams, teamNum: 1);
        if (orange is not null)
        {
            match.OrangeTeamName ??= orange.Name;
            match.OrangeColorPrimary ??= orange.ColorPrimary;
            match.OrangeColorSecondary ??= orange.ColorSecondary;
        }
    }

    private static void AddPlayerStats(StatsDbContext db, string matchGuid, MatchStateSnapshotData snapshot)
    {
        foreach (var player in snapshot.Players)
        {
            db.PlayerMatchStats.Add(new PlayerMatchStats
            {
                MatchGuid = matchGuid,
                Shortcut = player.Shortcut,
                PlayerName = player.Name,
                TeamNum = player.TeamNum,
                Platform = player.Platform,
                Score = player.Score,
                Goals = player.Goals,
                Assists = player.Assists,
                Saves = player.Saves,
                Shots = player.Shots,
                Touches = player.Touches,
            });
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
}
