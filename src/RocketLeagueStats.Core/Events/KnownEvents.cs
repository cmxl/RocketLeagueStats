namespace RocketLeagueStats.Core.Events;

/// <summary>
/// Wire-name constants for every event documented in the official Rocket League Stats API
/// (<c>https://www.rocketleague.com/en/developer/stats-api</c>). 19 events total.
/// </summary>
public static class KnownEvents
{
    // Discrete game events
    public const string BallHit = "BallHit";
    public const string CrossbarHit = "CrossbarHit";
    public const string GoalScored = "GoalScored";
    public const string StatfeedEvent = "StatfeedEvent";

    // Match lifecycle (MatchGuid-only)
    public const string MatchCreated = "MatchCreated";
    public const string MatchInitialized = "MatchInitialized";
    public const string MatchDestroyed = "MatchDestroyed";
    public const string MatchEnded = "MatchEnded";
    public const string MatchPaused = "MatchPaused";
    public const string MatchUnpaused = "MatchUnpaused";

    // Round / clock
    public const string CountdownBegin = "CountdownBegin";
    public const string RoundStarted = "RoundStarted";
    public const string ClockUpdatedSeconds = "ClockUpdatedSeconds";

    // Replay
    public const string GoalReplayStart = "GoalReplayStart";
    public const string GoalReplayWillEnd = "GoalReplayWillEnd";
    public const string GoalReplayEnd = "GoalReplayEnd";
    public const string ReplayCreated = "ReplayCreated";

    // Podium / periodic state
    public const string PodiumStart = "PodiumStart";
    public const string UpdateState = "UpdateState";

    // Observed on the wire but NOT in the docs — handled as MatchGuid-only markers.
    public const string ReplayPlaybackStart = "ReplayPlaybackStart";
    public const string ReplayWillEnd = "ReplayWillEnd";
    public const string ReplayPlaybackEnd = "ReplayPlaybackEnd";
}
