namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Summary of a completed match — used in history list and as the OnMatchEnded payload.</summary>
/// <remarks>
/// <see cref="BlueTeam"/>, <see cref="OrangeTeam"/>, and <see cref="ArenaName"/> are populated from
/// the persisted <c>Matches</c> row (which the writer fills at MatchEnded time from the latest
/// MatchStateSnapshot). They stay null on rows from before migration AddTeamMetadataAndPlayerStats.
/// <see cref="WinnerTeamNum"/> mirrors <c>Match.WinnerTeamNum</c> verbatim — 0 for blue, 1 for
/// orange, null only on abandoned matches that never received a MatchEnded event. Exposed
/// explicitly (rather than derived from BlueScore vs OrangeScore) because RL's overtime can end
/// the match the instant a goal is scored, and we want a single authoritative source of truth.
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
    string? ArenaName = null,
    int? WinnerTeamNum = null);
