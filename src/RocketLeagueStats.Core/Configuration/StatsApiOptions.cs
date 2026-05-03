namespace RocketLeagueStats.Core.Configuration;

public sealed class StatsApiOptions
{
    public const string SectionName = "StatsApi";

    public int Port { get; init; } = 49123;

    /// <summary>
    /// When true, <see cref="Connection.StatsApiClient"/> bypasses line-based parsing and instead logs every raw
    /// chunk read from the socket (length, hex preview, UTF-8 preview). Used to diagnose unknown wire framing.
    /// Discrete events are NOT published to the bus while trace mode is active.
    /// </summary>
    public bool TraceMode { get; init; }

    public ConnectRetryOptions ConnectRetry { get; init; } = new();

    public sealed class ConnectRetryOptions
    {
        public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum reconnect attempts before the listener gives up on the current pipeline run.
        /// Defaults to <see cref="int.MaxValue"/> so the WebApi keeps trying forever — users
        /// can browse history / recap UI even when Rocket League isn't running. The listener
        /// service no longer requests host shutdown when retries exhaust; it logs and re-enters
        /// the retry pipeline so a (theoretical) burst of upstream errors can't kill the app.
        /// Lower this only for narrow test scenarios that need bounded retry behaviour.
        /// </summary>
        public int MaxAttempts { get; init; } = int.MaxValue;
    }
}
