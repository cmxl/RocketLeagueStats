namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>The complete live state — used to bootstrap a freshly-connected client.</summary>
/// <remarks>
/// <see cref="Goals"/> and <see cref="Statfeeds"/> hold the full match history, newest first.
/// They are uncapped: clients receive every goal and statfeed of the active match on connect /
/// reconnect.
/// </remarks>
public sealed record LiveStateDto(
    MatchPhase Phase,
    MatchHeaderDto? CurrentMatch,
    int? CurrentMatchClockSeconds,
    int BlueScore,
    int OrangeScore,
    PlayerStatsRowDto[] PlayerStats,
    GoalDto[] Goals,
    StatfeedDto[] Statfeeds,
    DateTime? LastGoalAt,
    ConnectionStateDto Connection);
