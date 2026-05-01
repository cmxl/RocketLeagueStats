namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>The complete live state — used to bootstrap a freshly-connected client.</summary>
public sealed record LiveStateDto(
    MatchPhase Phase,
    MatchHeaderDto? CurrentMatch,
    int? CurrentMatchClockSeconds,
    int BlueScore,
    int OrangeScore,
    PlayerStatsRowDto[] PlayerStats,
    GoalDto[] RecentGoals,
    StatfeedDto[] RecentStatfeeds,
    DateTime? LastGoalAt,
    ConnectionStateDto Connection);
