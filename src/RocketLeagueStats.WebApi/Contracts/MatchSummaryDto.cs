namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Summary of a completed match — used in history list and as the OnMatchEnded payload.</summary>
/// <remarks>
/// <see cref="BlueTeam"/>, <see cref="OrangeTeam"/>, and <see cref="ArenaName"/> are populated from
/// the persisted <c>Matches</c> row (which the writer fills at MatchEnded time from the latest
/// MatchStateSnapshot). They stay null on rows from before migration AddTeamMetadataAndPlayerStats.
/// </remarks>
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
    GoalDto? FastestGoal,
    TeamDto? BlueTeam = null,
    TeamDto? OrangeTeam = null,
    string? ArenaName = null);
