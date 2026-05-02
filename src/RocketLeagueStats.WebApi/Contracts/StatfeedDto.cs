namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>
/// A statfeed event — saves, demolitions, epic saves, hattricks, etc.
/// <para>
/// <see cref="Type"/> is the enum bucket (used for aggregation and filtering).
/// <see cref="DisplayName"/> is RL's verbatim human label (e.g. <c>"Ultra Damage"</c>,
/// <c>"Bicycle Hit"</c>) — what we render in the UI.
/// </para>
/// </summary>
public sealed record StatfeedDto(
    DateTime Timestamp,
    int MatchClockSeconds,
    StatfeedType Type,
    string DisplayName,
    PlayerRefDto MainTarget,
    PlayerRefDto? SecondaryTarget);
