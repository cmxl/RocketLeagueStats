namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>A statfeed event — saves, demolitions, epic saves, hattricks, etc.</summary>
public sealed record StatfeedDto(
    DateTime Timestamp,
    int MatchClockSeconds,
    StatfeedType Type,
    PlayerRefDto MainTarget,
    PlayerRefDto? SecondaryTarget);
