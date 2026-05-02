namespace RocketLeagueStats.Core.Persistence.Entities;

public sealed class MatchSnapshotRecord
{
    public long Id { get; set; }

    public required string MatchGuid { get; set; }

    public long TimestampUtc { get; set; }

    public required string Payload { get; set; }
}
