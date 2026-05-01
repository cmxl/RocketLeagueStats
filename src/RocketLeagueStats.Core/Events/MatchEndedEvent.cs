namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Sent when the match ends and a winner is chosen. Per-team scores live on the final
/// <see cref="MatchStateSnapshot"/> rather than on this event.
/// </summary>
public sealed record MatchEndedEvent : StatsEvent
{
    [JsonPropertyName("WinnerTeamNum")]
    public int WinnerTeamNum { get; init; }
}
