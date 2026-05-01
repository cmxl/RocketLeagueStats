namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Sent when the ball hits a crossbar.
/// </summary>
public sealed record CrossbarHitEvent : StatsEvent
{
    [JsonPropertyName("BallLocation")]
    public Vec3 BallLocation { get; init; }

    [JsonPropertyName("BallSpeed")]
    public double BallSpeed { get; init; }

    [JsonPropertyName("ImpactForce")]
    public double ImpactForce { get; init; }

    [JsonPropertyName("BallLastTouch")]
    public BallLastTouchInfo? BallLastTouch { get; init; }
}
