namespace RocketLeagueStats.Core.Persistence.Entities;

public sealed class Match
{
    public required string MatchGuid { get; init; }

    public long FirstSeenAtUtc { get; set; }

    public long? CreatedAtUtc { get; set; }

    public long? InitializedAtUtc { get; set; }

    public long? EndedAtUtc { get; set; }

    public long? DestroyedAtUtc { get; set; }

    public int? WinnerTeamNum { get; set; }

    public long EventCount { get; set; }

    public long SnapshotCount { get; set; }

    public long LastEventAtUtc { get; set; }
}
