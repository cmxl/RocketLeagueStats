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

    // Team metadata + arena are nullable because rows written before migration
    // AddTeamMetadataAndPlayerStats existed without them. Populated at MatchEnded time by reading
    // the most recent MatchStateSnapshot for this match, so historical recaps can render real
    // colors / labels weeks after the fact instead of relying on the live wire being connected.
    public string? BlueTeamName { get; set; }

    public string? BlueColorPrimary { get; set; }

    public string? BlueColorSecondary { get; set; }

    public string? OrangeTeamName { get; set; }

    public string? OrangeColorPrimary { get; set; }

    public string? OrangeColorSecondary { get; set; }

    public string? Arena { get; set; }
}
