namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Header data for a match — identity, type, players.</summary>
public sealed record MatchHeaderDto(
    string MatchId,
    DateTime StartedAt,
    MatchType Type,
    string PlaylistRaw,
    PlayerRefDto[] BluePlayers,
    PlayerRefDto[] OrangePlayers,
    string? ArenaName);
