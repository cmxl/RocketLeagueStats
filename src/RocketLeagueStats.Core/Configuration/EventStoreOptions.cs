namespace RocketLeagueStats.Core.Configuration;

public sealed class EventStoreOptions
{
    public const string SectionName = "EventStore";

    public bool Enabled { get; init; } = true;

    public int MaxBatchSize { get; init; } = 200;

    public int MaxBatchLatencyMs { get; init; } = 250;
}
