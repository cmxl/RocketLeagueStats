namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>A single goal scored in a match.</summary>
public sealed record GoalDto(
    string Id,
    DateTime Timestamp,
    int MatchClockSeconds,
    PlayerRefDto Scorer,
    PlayerRefDto? Assister,
    double GoalSpeedUuPerSec,
    Vec3Dto ImpactLocation,
    int BlueScoreAfter,
    int OrangeScoreAfter,
    int? SecondsSinceLastGoal);
