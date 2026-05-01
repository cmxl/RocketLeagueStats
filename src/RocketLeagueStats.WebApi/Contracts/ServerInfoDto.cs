namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Server build/version metadata exposed via /api/info.</summary>
public sealed record ServerInfoDto(
    string Version,
    DateTime BuildDate,
    string[] EnabledFeatures);
