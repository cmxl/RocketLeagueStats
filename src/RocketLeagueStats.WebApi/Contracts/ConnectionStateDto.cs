namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>State of the API's connection to Rocket League's TCP Stats API.</summary>
public sealed record ConnectionStateDto(
    bool ConnectedToGame,
    DateTime? LastEventReceivedAt);
