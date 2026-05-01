namespace RocketLeagueStats.Core.Connection;

public interface IStatsApiClient
{
    /// <summary>
    /// Connects and reads JSON lines until the connection closes or cancellation fires.
    /// Throws on connect failure (caller decides retry policy).
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken);
}
