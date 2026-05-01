namespace RocketLeagueStats.WebApi.Hubs;

using RocketLeagueStats.WebApi.Contracts;

/// <summary>
/// Strongly-typed SignalR client contract — the methods the server pushes to browser clients.
/// Implemented by SignalR's runtime via the typed Hub&lt;T&gt; pattern.
/// </summary>
public interface IStatsHubClient
{
    /// <summary>A goal was scored.</summary>
    public Task OnGoal(GoalDto goal);

    /// <summary>A statfeed event (save, demo, epic save, etc.) occurred.</summary>
    public Task OnStatfeed(StatfeedDto statfeed);

    /// <summary>A new match started — fired on MatchInitialized. Resets all client-side state.</summary>
    public Task OnMatchInitialized(MatchHeaderDto header);

    /// <summary>
    /// The current match's roster grew (a new player was discovered in a goal/statfeed event).
    /// Carries the same MatchHeaderDto as OnMatchInitialized but fires mid-match — clients
    /// should patch only the header / roster, NOT reset scores or feeds.
    /// </summary>
    public Task OnRosterUpdated(MatchHeaderDto header);

    /// <summary>A match ended — fired on MatchEnded.</summary>
    public Task OnMatchEnded(MatchSummaryDto summary);

    /// <summary>Match clock tick — at most 1 Hz, only fired when integer-seconds value changes.</summary>
    public Task OnClockTick(int matchClockSeconds);

    /// <summary>Per-player running tallies — broadcast only when at least one row changes.</summary>
    public Task OnPlayerStatsTick(PlayerStatsRowDto[] rows);

    /// <summary>Connection state to RL's TCP API changed.</summary>
    public Task OnConnectionState(ConnectionStateDto state);

    /// <summary>Match phase changed (idle ↔ live).</summary>
    public Task OnPhaseChanged(MatchPhase phase);
}
