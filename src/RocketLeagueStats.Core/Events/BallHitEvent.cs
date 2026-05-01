namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Sent one frame after the ball is hit. Per the official Stats API docs, multiple players can
/// be credited on the same frame (hence the array).
/// </summary>
public sealed record BallHitEvent : StatsEvent
{
    [JsonPropertyName("Players")]
    public IReadOnlyList<PlayerRef> Players { get; init; } = [];

    [JsonPropertyName("Ball")]
    public BallHitState Ball { get; init; }

    public readonly record struct BallHitState(
        [property: JsonPropertyName("PreHitSpeed")] double PreHitSpeed,
        [property: JsonPropertyName("PostHitSpeed")] double PostHitSpeed,
        [property: JsonPropertyName("Location")] Vec3 Location);
}
