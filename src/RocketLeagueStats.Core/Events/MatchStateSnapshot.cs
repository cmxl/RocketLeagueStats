namespace RocketLeagueStats.Core.Events;

using System.Text.Json;

public sealed record MatchStateSnapshot : StatsEvent
{
    public required JsonElement RawData { get; init; }
}
