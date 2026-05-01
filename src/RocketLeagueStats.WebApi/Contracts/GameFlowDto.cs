namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Cumulative-score timeline for the recap game-flow chart.</summary>
public sealed record GameFlowDto(
    int[] TimestampSeconds,
    int[] BlueScoreAtStep,
    int[] OrangeScoreAtStep);
