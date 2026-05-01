namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>User-configurable settings (player name, friend list, history filter default).</summary>
public sealed record SettingsDto(
    string? PlayerName,
    string[] FriendNames,
    bool ShowTrainingInHistory);
