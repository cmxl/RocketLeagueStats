namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Shared "who last touched the ball" payload — appears on <see cref="GoalScoredEvent"/> and
/// <see cref="CrossbarHitEvent"/>. The <c>Speed</c> field is in Unreal Units / second per the docs.
/// </summary>
public readonly record struct BallLastTouchInfo(
    [property: JsonPropertyName("Player")] PlayerRef Player,
    [property: JsonPropertyName("Speed")] double Speed);
