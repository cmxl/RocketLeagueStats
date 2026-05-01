namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Sent when a goal is scored. Note: the running per-team score is NOT on this event — read it
/// from <see cref="MatchStateSnapshot"/>.<c>RawData.Game.Teams[].Score</c>.
/// </summary>
public sealed record GoalScoredEvent : StatsEvent
{
    [JsonPropertyName("GoalSpeed")]
    public double GoalSpeed { get; init; }

    [JsonPropertyName("GoalTime")]
    public double GoalTime { get; init; }

    [JsonPropertyName("ImpactLocation")]
    public Vec3 ImpactLocation { get; init; }

    [JsonPropertyName("Scorer")]
    public PlayerRef Scorer { get; init; }

    [JsonPropertyName("Assister")]
    public PlayerRef? Assister { get; init; }

    [JsonPropertyName("BallLastTouch")]
    public BallLastTouchInfo? BallLastTouch { get; init; }
}
