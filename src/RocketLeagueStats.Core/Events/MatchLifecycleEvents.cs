namespace RocketLeagueStats.Core.Events;

// Match-lifecycle events that carry only MatchGuid on the wire (per the official Stats API docs).
// Each is its own type so EventFormatter and downstream consumers can pattern-match by event kind.
// Inherits MatchGuid (and EventName/Timestamp) from StatsEvent — no extra fields.

/// <summary>Sent when all teams are created and replicated.</summary>
public sealed record MatchCreatedEvent : StatsEvent;

/// <summary>Sent when the first countdown of a match starts.</summary>
public sealed record MatchInitializedEvent : StatsEvent;

/// <summary>Sent when leaving the game.</summary>
public sealed record MatchDestroyedEvent : StatsEvent;

/// <summary>Sent when the game is paused by a match admin.</summary>
public sealed record MatchPausedEvent : StatsEvent;

/// <summary>Sent when the game is unpaused by a match admin.</summary>
public sealed record MatchUnpausedEvent : StatsEvent;

/// <summary>Sent at the start of each round when the countdown starts.</summary>
public sealed record CountdownBeginEvent : StatsEvent;

/// <summary>Sent when the game enters the active state after each countdown.</summary>
public sealed record RoundStartedEvent : StatsEvent;

/// <summary>Sent when a goal replay starts.</summary>
public sealed record GoalReplayStartEvent : StatsEvent;

/// <summary>Sent when the ball explodes during a goal replay (only fires if the replay is not skipped).</summary>
public sealed record GoalReplayWillEndEvent : StatsEvent;

/// <summary>Sent when a goal replay ends.</summary>
public sealed record GoalReplayEndEvent : StatsEvent;

/// <summary>Sent when a replay is initialized (Match History menu replays only — not goal replays).</summary>
public sealed record ReplayCreatedEvent : StatsEvent;

/// <summary>Sent when the game enters the podium state after the match ends.</summary>
public sealed record PodiumStartEvent : StatsEvent;

// The three records below are observed on the live wire but are NOT listed in the official docs
// (which only mention GoalReplayStart/End/WillEnd and ReplayCreated). They appear during goal
// replays alongside the documented GoalReplay* events. Treated as MatchGuid-only markers since
// that's all the JSONL captures for them.

/// <summary>Observed-on-wire (undocumented): fires when a replay begins playback.</summary>
public sealed record ReplayPlaybackStartEvent : StatsEvent;

/// <summary>Observed-on-wire (undocumented): fires shortly before a replay ends.</summary>
public sealed record ReplayWillEndEvent : StatsEvent;

/// <summary>Observed-on-wire (undocumented): fires when a replay ends playback.</summary>
public sealed record ReplayPlaybackEndEvent : StatsEvent;
