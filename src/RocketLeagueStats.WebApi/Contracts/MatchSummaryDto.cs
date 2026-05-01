namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Summary of a completed match — used in history list and as the OnMatchEnded payload.</summary>
public sealed record MatchSummaryDto(
    string MatchId,
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationSeconds,
    MatchType Type,
    int BlueScore,
    int OrangeScore,
    PlayerRefDto[] AllPlayers,
    PlayerRefDto? Mvp,
    int TotalGoals,
    GoalDto? FastestGoal);
