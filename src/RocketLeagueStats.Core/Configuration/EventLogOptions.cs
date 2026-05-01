namespace RocketLeagueStats.Core.Configuration;

public sealed class EventLogOptions
{
    public const string SectionName = "EventLog";

    public bool Enabled { get; init; } = true;
    public string? Directory { get; init; }
    public int RetentionDays { get; init; } = 7;
    public long MaxFileSizeBytes { get; init; } = 100L * 1024 * 1024;
}
