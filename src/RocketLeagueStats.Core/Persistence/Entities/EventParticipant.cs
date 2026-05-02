namespace RocketLeagueStats.Core.Persistence.Entities;

public sealed class EventParticipant
{
    public long EventId { get; set; }

    public required string MatchGuid { get; set; }

    public required string PlayerName { get; set; }

    public int Shortcut { get; set; }

    public int TeamNum { get; set; }

    public required string Role { get; set; }

    public long TimestampUtc { get; set; }
}
