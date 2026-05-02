namespace RocketLeagueStats.WebApi.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.Core.Persistence.Entities;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mapping;
using RocketLeagueStats.WebApi.Services.Recap;

/// <summary>
/// DB-backed read path for match history and recaps. The live projector no longer maintains an
/// in-memory index — every completed match is read straight from the SQLite event store the
/// writer service persists in the background. The live view's in-memory state is unaffected;
/// it remains the source of truth while a match is in progress, and this reader takes over the
/// instant the writer flushes the closing batch.
/// </summary>
internal sealed class MatchHistoryReader(StatsDbContext db)
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    // Re-capture the primary-constructor parameter as a real field so this.db. works under
    // IDE0009 (which expects instance qualification on field access).
    private readonly StatsDbContext db = db;

    public async Task<MatchSummaryDto[]> GetMatchesAsync(HistoryFilter filter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Empty MatchGuid is the wire's training/free-play marker — we filter at write time too,
        // but defend in depth here. Only completed matches (EndedAtUtc not null) appear in history.
        var matchesQuery = this.db.Matches
            .AsNoTracking()
            .Where(m => m.MatchGuid != string.Empty && m.EndedAtUtc != null);

        if (filter.From is { } from)
        {
            var fromMs = new DateTimeOffset(from, TimeSpan.Zero).ToUnixTimeMilliseconds();
            matchesQuery = matchesQuery.Where(m => m.FirstSeenAtUtc >= fromMs);
        }

        if (filter.To is { } to)
        {
            var toMs = new DateTimeOffset(to, TimeSpan.Zero).ToUnixTimeMilliseconds();
            matchesQuery = matchesQuery.Where(m => m.EndedAtUtc <= toMs);
        }

        var matches = await matchesQuery.ToListAsync(ct);
        if (matches.Count == 0)
        {
            return [];
        }

        var matchGuids = matches.Select(m => m.MatchGuid).ToList();
        var participantsByMatch = await this.GetParticipantsByMatchAsync(matchGuids, ct);
        var goalsByMatch = await this.GetGoalsByMatchAsync(matchGuids, ct);

        var summaries = new List<MatchSummaryDto>(matches.Count);
        foreach (var match in matches)
        {
            participantsByMatch.TryGetValue(match.MatchGuid, out var matchParticipants);
            goalsByMatch.TryGetValue(match.MatchGuid, out var matchGoals);
            summaries.Add(BuildSummary(match, matchParticipants ?? [], matchGoals ?? []));
        }

        var ordered = filter.Sort switch
        {
            HistorySort.MostRecent => summaries.OrderByDescending(s => s.EndedAt),
            HistorySort.HighestScoring => summaries.OrderByDescending(s => s.TotalGoals),
            _ => summaries.AsEnumerable(),
        };

        return [.. ordered];
    }

    public async Task<MatchRecapDto?> GetRecapAsync(string matchId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(matchId);

        var match = await this.db.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MatchGuid == matchId, ct);
        if (match is null || match.EndedAtUtc is null)
        {
            return null;
        }

        var participants = (await this.GetParticipantsByMatchAsync([match.MatchGuid], ct))
            .GetValueOrDefault(match.MatchGuid, []);

        var rawEvents = await this.db.Events
            .AsNoTracking()
            .Where(e => e.MatchGuid == match.MatchGuid &&
                (e.EventName == KnownEvents.GoalScored || e.EventName == KnownEvents.StatfeedEvent))
            .OrderBy(e => e.TimestampUtc)
            .Select(e => new EventRow(e.EventName, e.TimestampUtc, e.Payload))
            .ToListAsync(ct);

        var nonPhantomGoals = rawEvents
            .Where(e => e.EventName == KnownEvents.GoalScored)
            .Select(e => DeserializeGoal(e.Payload))
            .Where(g => g is not null && !IsKickoffPhantom(g))
            .Cast<GoalScoredEvent>()
            .ToList();

        var summary = BuildSummary(match, participants, nonPhantomGoals);

        var goalDtos = BuildGoalDtos(rawEvents, match.FirstSeenAtUtc).ToList();
        var statfeedDtos = BuildStatfeedDtos(rawEvents, match.FirstSeenAtUtc).ToList();
        var playerStats = PlayerTallyAggregator.Aggregate(summary.AllPlayers, goalDtos, statfeedDtos, markMvp: true);
        var mvp = playerStats.FirstOrDefault(r => r.IsMvp)?.Player;

        return new MatchRecapDto(
            Summary: summary with { Mvp = mvp },
            Goals: [.. goalDtos],
            Statfeeds: [.. statfeedDtos],
            PlayerStats: playerStats,
            TimeBetweenGoalsSeconds: ComputeTimeBetweenGoals(goalDtos),
            Flow: BuildFlow(summary, goalDtos));
    }

    private async Task<Dictionary<string, List<ParticipantRow>>> GetParticipantsByMatchAsync(
        List<string> matchGuids,
        CancellationToken ct)
    {
        var rows = await this.db.EventParticipants
            .AsNoTracking()
            .Where(p => matchGuids.Contains(p.MatchGuid))
            .Select(p => new ParticipantRow(p.MatchGuid, p.PlayerName, p.Shortcut, p.TeamNum))
            .Distinct()
            .ToListAsync(ct);

        return rows
            .GroupBy(p => p.MatchGuid)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<Dictionary<string, List<GoalScoredEvent>>> GetGoalsByMatchAsync(
        List<string> matchGuids,
        CancellationToken ct)
    {
        var rows = await this.db.Events
            .AsNoTracking()
            .Where(e => e.EventName == KnownEvents.GoalScored && e.MatchGuid != null && matchGuids.Contains(e.MatchGuid))
            .OrderBy(e => e.TimestampUtc)
            .Select(e => new GoalRow(e.MatchGuid!, e.TimestampUtc, e.Payload))
            .ToListAsync(ct);

        var result = new Dictionary<string, List<GoalScoredEvent>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var goal = DeserializeGoal(row.Payload);
            if (goal is null || IsKickoffPhantom(goal))
            {
                continue;
            }

            if (!result.TryGetValue(row.MatchGuid, out var list))
            {
                list = [];
                result[row.MatchGuid] = list;
            }

            list.Add(goal);
        }

        return result;
    }

    // We don't persist playlist info today, so MatchType is hard-coded to Online here. Training /
    // free-play / private modes (empty MatchGuid on the wire) are filtered at write time so anything
    // reaching the DB is an Online-class match. Future enhancement: persist a Type column on Matches.
    private static MatchSummaryDto BuildSummary(
        Match match,
        List<ParticipantRow> participants,
        List<GoalScoredEvent> nonPhantomGoals)
    {
        var allPlayers = MapParticipantsForRoster(participants);
        var blueScore = nonPhantomGoals.Count(g => g.Scorer.TeamNum == 0);
        var orangeScore = nonPhantomGoals.Count(g => g.Scorer.TeamNum == 1);

        GoalDto? fastestGoal = null;
        if (nonPhantomGoals.Count > 0)
        {
            var fastest = nonPhantomGoals.OrderByDescending(g => g.GoalSpeed).First();
            fastestGoal = EventMapper.ToDto(fastest, (int)Math.Round(fastest.GoalTime), null);
        }

        var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(match.FirstSeenAtUtc).UtcDateTime;
        var endedAt = DateTimeOffset.FromUnixTimeMilliseconds(match.EndedAtUtc!.Value).UtcDateTime;
        var durationSeconds = (int)Math.Max(0, (endedAt - startedAt).TotalSeconds);

        return new MatchSummaryDto(
            MatchId: match.MatchGuid,
            StartedAt: startedAt,
            EndedAt: endedAt,
            DurationSeconds: durationSeconds,
            Type: MatchType.Online,
            BlueScore: blueScore,
            OrangeScore: orangeScore,
            AllPlayers: allPlayers,
            Mvp: null,
            TotalGoals: nonPhantomGoals.Count,
            FastestGoal: fastestGoal);
    }

    // Platform isn't persisted on EventParticipants today — populating it would require parsing the
    // last MatchSnapshot for the match. Historical recaps return an empty Platform string; the
    // frontend renders the platform pill conditionally so absent values just hide it.
    private static PlayerRefDto[] MapParticipantsForRoster(List<ParticipantRow> rows) =>
        [.. rows
            .GroupBy(r => r.Shortcut)
            .Select(g =>
            {
                var first = g.First();
                var team = first.TeamNum switch
                {
                    0 => "blue",
                    1 => "orange",
                    _ => "unknown",
                };
                return new PlayerRefDto(first.PlayerName, g.Key, team, Platform: string.Empty);
            })
            .OrderBy(p => p.Shortcut)];

    private static IEnumerable<GoalDto> BuildGoalDtos(List<EventRow> rawEvents, long matchStartUtc)
    {
        var blue = 0;
        var orange = 0;
        foreach (var row in rawEvents.Where(r => r.EventName == KnownEvents.GoalScored).OrderBy(r => r.TimestampUtc))
        {
            var goal = DeserializeGoal(row.Payload);
            if (goal is null || IsKickoffPhantom(goal))
            {
                continue;
            }

            if (goal.Scorer.TeamNum == 0)
            {
                blue++;
            }
            else if (goal.Scorer.TeamNum == 1)
            {
                orange++;
            }

            var matchClock = (int)Math.Max(0, (row.TimestampUtc - matchStartUtc) / 1000);
            yield return EventMapper.ToDto(goal, matchClock, secondsSinceLastGoal: null) with
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(row.TimestampUtc).UtcDateTime,
                BlueScoreAfter = blue,
                OrangeScoreAfter = orange,
            };
        }
    }

    private static IEnumerable<StatfeedDto> BuildStatfeedDtos(List<EventRow> rawEvents, long matchStartUtc)
    {
        foreach (var row in rawEvents.Where(r => r.EventName == KnownEvents.StatfeedEvent).OrderBy(r => r.TimestampUtc))
        {
            StatfeedEvent? stat;
            try
            {
                stat = JsonSerializer.Deserialize<StatfeedEvent>(row.Payload, PayloadJsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (stat is null)
            {
                continue;
            }

            // Skip statfeeds duplicated by an explicit GoalScored entry — matches the live
            // projector's logic (LiveMatchProjector.HandleStatfeedAsync).
            if (stat.StatName is "Goal" or "Assist")
            {
                continue;
            }

            var matchClock = (int)Math.Max(0, (row.TimestampUtc - matchStartUtc) / 1000);
            yield return EventMapper.ToDto(stat, matchClock) with
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(row.TimestampUtc).UtcDateTime,
            };
        }
    }

    private static int[] ComputeTimeBetweenGoals(List<GoalDto> goals)
    {
        if (goals.Count == 0)
        {
            return [];
        }

        var sorted = goals.OrderBy(g => g.MatchClockSeconds).ToList();
        var result = new int[sorted.Count];
        result[0] = sorted[0].MatchClockSeconds;
        for (var i = 1; i < sorted.Count; i++)
        {
            result[i] = sorted[i].MatchClockSeconds - sorted[i - 1].MatchClockSeconds;
        }

        return result;
    }

    private static GameFlowDto BuildFlow(MatchSummaryDto summary, List<GoalDto> goals)
    {
        if (goals.Count == 0)
        {
            return new GameFlowDto(
                TimestampSeconds: [0, summary.DurationSeconds],
                BlueScoreAtStep: [0, summary.BlueScore],
                OrangeScoreAtStep: [0, summary.OrangeScore]);
        }

        var sorted = goals.OrderBy(g => g.MatchClockSeconds).ToList();
        var times = new List<int> { 0 };
        var blue = new List<int> { 0 };
        var orange = new List<int> { 0 };
        var b = 0;
        var o = 0;
        foreach (var g in sorted)
        {
            if (g.Scorer.Team == "blue")
            {
                b++;
            }
            else if (g.Scorer.Team == "orange")
            {
                o++;
            }

            times.Add(g.MatchClockSeconds);
            blue.Add(b);
            orange.Add(o);
        }

        times.Add(summary.DurationSeconds);
        blue.Add(b);
        orange.Add(o);

        return new GameFlowDto([.. times], [.. blue], [.. orange]);
    }

    private static GoalScoredEvent? DeserializeGoal(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<GoalScoredEvent>(payload, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsKickoffPhantom(GoalScoredEvent evt) =>
        string.IsNullOrEmpty(evt.Scorer.Name) && evt.GoalSpeed == 0;

    private sealed record ParticipantRow(string MatchGuid, string PlayerName, int Shortcut, int TeamNum);

    private sealed record GoalRow(string MatchGuid, long TimestampUtc, string Payload);

    private sealed record EventRow(string EventName, long TimestampUtc, string Payload);
}
