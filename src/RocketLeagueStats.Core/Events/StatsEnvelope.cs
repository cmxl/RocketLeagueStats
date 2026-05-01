namespace RocketLeagueStats.Core.Events;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record StatsEnvelope
{
    [JsonPropertyName("Event")]
    public required string Event { get; init; }

    [JsonPropertyName("Data")]
    public JsonElement Data { get; init; }

    [JsonPropertyName("Timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("MatchGuid")]
    public string? MatchGuid { get; init; }
}
