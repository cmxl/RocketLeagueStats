namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Minimal identifier the Stats API uses inside discrete-event payloads (BallHit, GoalScored,
/// StatfeedEvent). Match-scope state in <see cref="MatchStateSnapshot"/> carries a richer player
/// shape (PrimaryId, Score, Boost, etc.) and is intentionally left as raw JSON.
/// </summary>
public readonly record struct PlayerRef(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("Shortcut")] int Shortcut,
    [property: JsonPropertyName("TeamNum")] int TeamNum);
