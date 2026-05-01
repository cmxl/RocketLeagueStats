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
    IMatchHistoryIndex history,
    ILogger<LiveMatchProjector> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch event of type {EventType}")]
    private static partial void LogDispatchError(ILogger logger, Exception ex, string eventType);
    private string? currentMatchId;
    private int currentClockSeconds;
    private int lastBroadcastClockSeconds = -1;
    private DateTime? lastGoalTimestamp;
    private PlayerStatsRowDto[] lastBroadcastPlayerStats = [];

    // Roster accumulator: built up from PlayerRefs that appear in goal/statfeed events. RL's
    // MatchInitialized doesn't carry the roster (only MatchGuid), and MatchStateSnapshot's
    // wire format is not yet parsed — so we discover players lazily as they generate events.
    // Cleared on each MatchInitialized; keyed by Shortcut (RL's stable per-match player int id).
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
            default:
                break;
        }
    }

    private async Task HandleMatchInitializedAsync(MatchInitializedEvent evt)
    {
        var matchId = Guid.NewGuid().ToString();
        this.currentMatchId = matchId;
        this.currentClockSeconds = 0;
        this.lastGoalTimestamp = null;
        this.seenPlayers.Clear();

        // Coarse classification from MatchGuid presence: real online matches (ranked / casual /
        // tournament) have a non-empty MatchGuid; offline modes (training / freeplay / private)
        // leave it empty. Specific types (Ranked3v3, Training, etc.) get refined later when the
        // MatchStateSnapshot parser lands.
        var coarseType = string.IsNullOrEmpty(evt.MatchGuid) ? MatchType.Offline : MatchType.Online;

        var header = new MatchHeaderDto(
            MatchId: matchId,
            StartedAt: DateTime.UtcNow,
            Type: coarseType,
            PlaylistRaw: string.Empty,
            BluePlayers: [],
            OrangePlayers: [],
            ArenaName: null);

        state.BeginMatch(header);
        history.BeginMatch(header);
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

        history.CompleteMatch(summary.MatchId, summary);
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
        var stamped = state.RecentGoals[0];
        history.AppendGoal(this.currentMatchId, stamped);
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

        var dto = EventMapper.ToDto(evt, this.currentClockSeconds);
        state.AppendStatfeed(dto);
        history.AppendStatfeed(this.currentMatchId, dto);

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

    private async Task HandleClockAsync(ClockUpdatedSecondsEvent evt)
    {
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
