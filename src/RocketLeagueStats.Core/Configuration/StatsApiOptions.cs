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
        /// Maximum reconnect attempts before the host exits. With the default 30 attempts and 30s
        /// max delay, this caps the reconnect storm at ~12 minutes of "Rocket League is closed" before
        /// the process exits cleanly. Set to <see cref="int.MaxValue"/> to retry forever.
        /// </summary>
        public int MaxAttempts { get; init; } = 30;
    }
}
