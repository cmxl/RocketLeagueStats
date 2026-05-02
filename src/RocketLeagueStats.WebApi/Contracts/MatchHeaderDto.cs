namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Header data for a match — identity, type, players, team metadata.</summary>
/// <remarks>
/// <see cref="BlueTeam"/> and <see cref="OrangeTeam"/> are populated from the first
/// <c>MatchStateSnapshot</c> tick of a match (not from <c>MatchInitialized</c>, which only
/// carries <c>MatchGuid</c>). They stay null until that first snapshot arrives — usually within
/// a second of the listener connecting.
/// </remarks>
public sealed record MatchHeaderDto(
    string MatchId,
    DateTime StartedAt,
    MatchType Type,
    string PlaylistRaw,
    PlayerRefDto[] BluePlayers,
    PlayerRefDto[] OrangePlayers,
    string? ArenaName,
    TeamDto? BlueTeam = null,
    TeamDto? OrangeTeam = null);
