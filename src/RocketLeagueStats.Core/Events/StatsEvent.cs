namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

public abstract record StatsEvent
{
    /// <summary>
    /// The wire envelope's <c>Event</c> name (e.g. "GoalScored"). Set by the parser via <c>with</c>.
    /// Mapped to <c>"Event"</c> on the JSON wire (matching the live plugin's envelope) so JSONL replay
    /// tooling can identify each line by its top-level <c>Event</c> field.
    /// </summary>
    [JsonPropertyName("Event")]
    public string EventName { get; init; } = string.Empty;

    public DateTimeOffset? Timestamp { get; init; }

    public string? MatchGuid { get; init; }
}
