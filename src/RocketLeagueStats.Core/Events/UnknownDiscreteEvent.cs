namespace RocketLeagueStats.Core.Events;

using System.Text.Json;

public sealed record UnknownDiscreteEvent : StatsEvent
{
    public required JsonElement RawData { get; init; }
}
