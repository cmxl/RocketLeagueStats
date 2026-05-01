namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Fired whenever a player earns a stat (demolish, save, epic-save, …). The stat asset name is
/// in <see cref="StatName"/> (mapped from the wire's inner <c>"EventName"</c> field — note the
/// collision with <see cref="StatsEvent.EventName"/>, which is the outer envelope name).
/// </summary>
public sealed record StatfeedEvent : StatsEvent
{
    [JsonPropertyName("EventName")]
    public string StatName { get; init; } = string.Empty;

    [JsonPropertyName("Type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("MainTarget")]
    public PlayerRef MainTarget { get; init; }

    [JsonPropertyName("SecondaryTarget")]
    public PlayerRef? SecondaryTarget { get; init; }
}
