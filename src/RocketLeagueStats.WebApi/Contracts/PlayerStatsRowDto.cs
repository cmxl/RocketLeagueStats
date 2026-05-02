namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Per-player aggregated stats in a match.</summary>
/// <remarks>
/// <see cref="Score"/> and <see cref="Touches"/> come from the live MatchStateSnapshot wire and
/// are the authoritative wire-side values — they're 0 in recap aggregation (which works from
/// persisted goal/statfeed events) and non-zero in the live view once the first snapshot lands.
/// <see cref="Goals"/>/<see cref="Assists"/>/<see cref="Saves"/>/<see cref="Shots"/> are also
/// snapshot-overridden in the live view; the other fields (EpicSaves, demos, crossbar hits,
/// fastest goal) stay event-derived everywhere because the snapshot doesn't carry them.
/// <see cref="MvpScore"/> is a synthetic ranking number used for MVP highlight only.
/// </remarks>
public sealed record PlayerStatsRowDto(
    PlayerRefDto Player,
    int Goals,
    int Assists,
    int Saves,
    int EpicSaves,
    int Shots,
    int DemosInflicted,
    int DemosTaken,
    int CrossbarHits,
    double FastestGoalSpeedUuPerSec,
    double MvpScore,
    bool IsMvp,
    int Score = 0,
    int Touches = 0);
