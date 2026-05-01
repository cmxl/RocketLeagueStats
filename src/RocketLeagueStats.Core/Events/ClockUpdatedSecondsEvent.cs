namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Sent when the in-game clock has changed.
/// </summary>
public sealed record ClockUpdatedSecondsEvent : StatsEvent
{
    [JsonPropertyName("TimeSeconds")]
    public int TimeSeconds { get; init; }

    [JsonPropertyName("bOvertime")]
    public bool Overtime { get; init; }
}
