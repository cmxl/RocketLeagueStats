namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Full recap data for a completed match.</summary>
public sealed record MatchRecapDto(
    MatchSummaryDto Summary,
    GoalDto[] Goals,
    StatfeedDto[] Statfeeds,
    PlayerStatsRowDto[] PlayerStats,
    int[] TimeBetweenGoalsSeconds,
    GameFlowDto Flow);
