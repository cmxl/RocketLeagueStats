namespace RocketLeagueStats.Core.Persistence.Entities;

public sealed class EventRecord
{
    public long Id { get; set; }

    public string? MatchGuid { get; set; }

    public required string EventName { get; set; }

    public long TimestampUtc { get; set; }

    public required string Payload { get; set; }
}
