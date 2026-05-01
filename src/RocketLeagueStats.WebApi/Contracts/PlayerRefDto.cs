namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>A reference to a player within a match, mapped from RL's internal PlayerRef.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Shortcut">RL's stable per-match player int identifier; disambiguates same-name players.</param>
/// <param name="Team">"blue" or "orange" (or "unknown" if mapping failed).</param>
public sealed record PlayerRefDto(string Name, int Shortcut, string Team);
