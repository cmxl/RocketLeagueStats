namespace RocketLeagueStats.WebApi.Services;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Hubs;
using RocketLeagueStats.WebApi.Mapping;

internal sealed partial class LiveMatchProjector(
    StatsEventBus bus,
    IHubContext<StatsHub, IStatsHubClient> hub,
    LiveMatchState state,
    ILogger<LiveMatchProjector> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch event of type {EventType}")]
    private static partial void LogDispatchError(ILogger logger, Exception ex, string eventType);
    private string? currentMatchId;
    private int currentClockSeconds;
    private int lastBroadcastClockSeconds = -1;
    private DateTime? lastGoalTimestamp;
    private PlayerStatsRowDto[] lastBroadcastPlayerStats = [];

    // First-snapshot-per-match flag. Snapshots fire at ~30Hz; we only enrich the header from the
    // first one because the fields we extract (roster, team metadata, arena) are stable for the
    // duration of a match. Reset on each MatchInitialized.
    private bool enrichedFromSnapshot;

    // Roster accumulator: built up from PlayerRefs that appear in goal/statfeed events. Used as a
    // fallback when MatchStateSnapshot enrichment hasn't happened yet (e.g., very first goal of a
    // match arrives before the first snapshot tick) or when a player joins mid-match and is
    // discovered via a discrete event before the next snapshot lands. Cleared on MatchInitialized.
    private readonly Dictionary<int, PlayerRefDto> seenPlayers = [];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var reader = bus.Subscribe();
        try
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
            {
                try
                {
                    await this.DispatchAsync(evt);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogDispatchError(logger, ex, evt.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DispatchAsync(StatsEvent evt)
    {
        switch (evt)
        {
            case MatchInitializedEvent init:
                await this.HandleMatchInitializedAsync(init);
                break;
            case MatchEndedEvent:
            case MatchDestroyedEvent:
                // Both events signal a match concluding. Ranked matches fire MatchEnded
                // followed by MatchDestroyed; training / free-play matches only fire
                // MatchDestroyed (no MatchEnded). The handler is idempotent — if state
                // is already idle from a prior MatchEnded, the second call is a no-op.
                await this.HandleMatchConcludedAsync();
                break;
            case GoalScoredEvent goal:
                await this.HandleGoalAsync(goal);
                break;
            case StatfeedEvent statfeed:
                await this.HandleStatfeedAsync(statfeed);
                break;
            case ClockUpdatedSecondsEvent clock:
                await this.HandleClockAsync(clock);
                break;
            case MatchStateSnapshot snapshot:
                await this.HandleSnapshotAsync(snapshot);
                break;
            default:
                break;
        }
    }

    private async Task HandleMatchInitializedAsync(MatchInitializedEvent evt)
    {
        // Training / free-play / private-match events arrive with an empty MatchGuid. By project
        // policy we don't track those as live matches — no live UI, no history, no recap. Returning
        // early keeps `currentMatchId` null so the downstream HandleGoal / HandleStatfeed /
        // HandleClock methods short-circuit on subsequent events from this offline session.
        if (string.IsNullOrEmpty(evt.MatchGuid))
        {
            return;
        }

        // Use the wire's MatchGuid directly as our MatchId. This is the same key under which the
        // SqliteEventStoreService persists rows, so the live UI's recap link
        // (/api/matches/{matchId}) resolves cleanly once the writer flushes the closing batch.
        // Until commit c2c2dc3 we minted a synthetic Guid here, which 404'd whenever a user clicked
        // "show recap" from the live view because DB-backed history is keyed by wire MatchGuid.
        var matchId = evt.MatchGuid;
        this.currentMatchId = matchId;
        this.currentClockSeconds = 0;
        this.lastGoalTimestamp = null;
        this.seenPlayers.Clear();
        this.enrichedFromSnapshot = false;

        // Specific match types (Ranked3v3, Casual, etc.) get refined later when the
        // MatchStateSnapshot parser lands; until then everything we track here is "Online".
        var coarseType = MatchType.Online;

        var header = new MatchHeaderDto(
            MatchId: matchId,
            StartedAt: DateTime.UtcNow,
            Type: coarseType,
            PlaylistRaw: string.Empty,
            BluePlayers: [],
            OrangePlayers: [],
            ArenaName: null);

        state.BeginMatch(header);
        this.lastBroadcastPlayerStats = [];

        await hub.Clients.All.OnMatchInitialized(header);
        await hub.Clients.All.OnPhaseChanged(MatchPhase.Live);
    }

    private async Task HandleMatchConcludedAsync()
    {
        var summary = state.EndMatch();
        if (summary is null)
        {
            return;
        }

        this.currentMatchId = null;

        await hub.Clients.All.OnMatchEnded(summary);
        await hub.Clients.All.OnPhaseChanged(MatchPhase.Idle);
    }

    private async Task HandleGoalAsync(GoalScoredEvent evt)
    {
        if (this.currentMatchId is null)
        {
            return;
        }

        // Suppress the kickoff phantom. In ranked / competitive matches Rocket League's
        // Stats API fires a second GoalScored event ~5-15s after each real goal — at
        // round restart. The phantom always has GoalSpeed=0, GoalTime=0, and an empty
        // Scorer (Name="", Shortcut=0, TeamNum=0). We can't filter on empty scorer
        // alone: a team-attributed own-goal (when no opponent ever touched the ball)
        // also has an empty scorer — but it carries a real impact speed. Require BOTH
        // empty scorer AND GoalSpeed=0 to identify the kickoff phantom.
        if (IsKickoffPhantom(evt))
        {
            return;
        }

        int? secondsSinceLastGoal = this.lastGoalTimestamp is { } prev
            ? (int)(DateTime.UtcNow - prev).TotalSeconds
            : this.currentClockSeconds;

        var dto = EventMapper.ToDto(evt, this.currentClockSeconds, secondsSinceLastGoal);
        state.AppendGoal(dto);
        var stamped = state.Goals[0];
        this.lastGoalTimestamp = stamped.Timestamp;

        await hub.Clients.All.OnGoal(stamped);
        await this.MaybeUpdateRosterAsync(stamped.Scorer, stamped.Assister);
        await this.MaybeBroadcastPlayerStatsAsync();
    }

    private async Task HandleStatfeedAsync(StatfeedEvent evt)
    {
        if (this.currentMatchId is null)
        {
            return;
        }

        // Skip statfeeds that duplicate signal already carried by GoalScoredEvent. RL fires a
        // "Goal" statfeed for every scored goal and an "Assist" statfeed for every assisted goal,
        // both of which we already capture with full context (speed, location, score-after) on
        // the GoalDto path. The qualifier variants — AerialGoal, BackwardsGoal, OvertimeGoal —
        // are kept; they enrich beyond what GoalScoredEvent carries.
        if (evt.StatName is "Goal" or "Assist")
        {
            return;
        }

        var dto = EventMapper.ToDto(evt, this.currentClockSeconds);
        state.AppendStatfeed(dto);

        await hub.Clients.All.OnStatfeed(dto);
        await this.MaybeUpdateRosterAsync(dto.MainTarget, dto.SecondaryTarget);
        await this.MaybeBroadcastPlayerStatsAsync();
    }

    /// <summary>
    /// Adds any new players to the seen-roster dictionary and, if the roster grew, broadcasts
    /// an OnRosterUpdated with the updated header so clients can render player cards lazily.
    /// </summary>
    private async Task MaybeUpdateRosterAsync(params PlayerRefDto?[] players)
    {
        var grew = false;
        foreach (var p in players)
        {
            if (p is null)
            {
                continue;
            }

            // Skip empty / unknown-team entries — these are kickoff-phantom or malformed
            // PlayerRefs we don't want polluting the roster.
            if (string.IsNullOrEmpty(p.Name) || (p.Team != "blue" && p.Team != "orange"))
            {
                continue;
            }

            if (this.seenPlayers.TryAdd(p.Shortcut, p))
            {
                grew = true;
            }
        }

        if (!grew)
        {
            return;
        }

        var blue = this.seenPlayers.Values.Where(p => p.Team == "blue").ToArray();
        var orange = this.seenPlayers.Values.Where(p => p.Team == "orange").ToArray();
        var updated = state.UpdateRoster(blue, orange);
        if (updated is not null)
        {
            await hub.Clients.All.OnRosterUpdated(updated);
        }
    }

    private async Task HandleSnapshotAsync(MatchStateSnapshot snapshot)
    {
        // Snapshot processing only applies once we have an active live match. Snapshots that arrive
        // during the post-game podium screen (after MatchEnded/MatchDestroyed but before the next
        // MatchInitialized) are ignored.
        if (this.currentMatchId is null)
        {
            return;
        }

        if (!MatchStateSnapshotData.TryParse(snapshot.RawData, out var data) || data is null)
        {
            return;
        }

        // Per-tick: refresh the snapshot-derived per-player stat overrides so the next
        // OnPlayerStatsTick broadcast carries wire-authoritative Goals/Assists/Saves/Shots/Score/
        // Touches. Cheap dictionary build; broadcast itself is deduped via PlayerStatsEqual.
        var snapshotOverrides = new Dictionary<int, SnapshotPlayer>(data.Players.Count);
        foreach (var p in data.Players)
        {
            snapshotOverrides[p.Shortcut] = p;
        }

        state.SetSnapshotPlayerOverrides(snapshotOverrides);

        // First-tick-only: enrich the header with roster + team colors + arena. These fields are
        // stable for a match's duration so re-broadcasting OnRosterUpdated at 30Hz would be waste.
        if (!this.enrichedFromSnapshot)
        {
            var (blueTeam, orangeTeam) = (FindTeam(data.Teams, teamNum: 0), FindTeam(data.Teams, teamNum: 1));
            var bluePlayers = data.Players
                .Where(p => p.TeamNum == 0)
                .Select(SnapshotPlayerToDto)
                .ToArray();
            var orangePlayers = data.Players
                .Where(p => p.TeamNum == 1)
                .Select(SnapshotPlayerToDto)
                .ToArray();

            // Sync the lazy-discovery accumulator with the authoritative snapshot roster so any
            // future discrete event from a player who somehow wasn't in the snapshot still triggers
            // a roster update via MaybeUpdateRosterAsync.
            this.seenPlayers.Clear();
            foreach (var p in bluePlayers.Concat(orangePlayers))
            {
                this.seenPlayers[p.Shortcut] = p;
            }

            var enriched = state.EnrichFromSnapshot(bluePlayers, orangePlayers, blueTeam, orangeTeam, data.Arena);
            if (enriched is not null)
            {
                this.enrichedFromSnapshot = true;
                await hub.Clients.All.OnRosterUpdated(enriched);
            }
        }

        await this.MaybeBroadcastPlayerStatsAsync();
    }

    private static TeamDto? FindTeam(IReadOnlyList<SnapshotTeam> teams, int teamNum)
    {
        for (var i = 0; i < teams.Count; i++)
        {
            if (teams[i].TeamNum == teamNum)
            {
                return new TeamDto(teams[i].Name, teams[i].ColorPrimary, teams[i].ColorSecondary);
            }
        }

        return null;
    }

    private static PlayerRefDto SnapshotPlayerToDto(SnapshotPlayer p) =>
        new(
            Name: p.Name,
            Shortcut: p.Shortcut,
            Team: p.TeamNum switch { 0 => "blue", 1 => "orange", _ => "unknown" },
            Platform: p.Platform);

    private async Task HandleClockAsync(ClockUpdatedSecondsEvent evt)
    {
        // Gate identical to HandleGoalAsync / HandleStatfeedAsync / HandleSnapshotAsync. Without
        // it, training / free-play / private-match clock ticks (which the wire emits with an empty
        // MatchGuid that the bus-read filter already drops at MatchInitialized time) would still
        // reach this handler — and broadcasting OnClockTick during training looks to the user like
        // a phantom live match has started. By project policy offline modes drive zero live UI.
        if (this.currentMatchId is null)
        {
            return;
        }

        this.currentClockSeconds = evt.TimeSeconds;
        state.UpdateClock(evt.TimeSeconds);
        if (evt.TimeSeconds != this.lastBroadcastClockSeconds)
        {
            this.lastBroadcastClockSeconds = evt.TimeSeconds;
            await hub.Clients.All.OnClockTick(evt.TimeSeconds);
        }
    }

    private async Task MaybeBroadcastPlayerStatsAsync()
    {
        var rows = state.CurrentPlayerStats();
        if (PlayerStatsEqual(rows, this.lastBroadcastPlayerStats))
        {
            return;
        }

        this.lastBroadcastPlayerStats = rows;
        await hub.Clients.All.OnPlayerStatsTick(rows);
    }

    /// <summary>
    /// Detects the post-goal kickoff "phantom" GoalScored event the official Stats API
    /// emits in ranked matches. Requires BOTH an empty scorer name AND GoalSpeed=0 —
    /// either alone is ambiguous (team-attributed own-goals also have empty scorers
    /// but carry a real impact speed; a slow legitimate goal still has a named scorer).
    /// </summary>
    internal static bool IsKickoffPhantom(GoalScoredEvent evt) =>
        string.IsNullOrEmpty(evt.Scorer.Name) && evt.GoalSpeed == 0;

    private static bool PlayerStatsEqual(PlayerStatsRowDto[] a, PlayerStatsRowDto[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
