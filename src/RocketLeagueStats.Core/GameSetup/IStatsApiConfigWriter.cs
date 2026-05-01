namespace RocketLeagueStats.Core.GameSetup;

public sealed record StatsApiConfigDesired(int PacketSendRate, int Port);

public sealed record StatsApiConfigOutcome(bool Changed, IReadOnlyList<string> ChangedKeys, string Reason);

public interface IStatsApiConfigWriter
{
    /// <summary>
    /// Inspects DefaultStatsAPI.ini under the given install path, computes a diff against
    /// the desired settings, and writes the file when out-of-date.
    ///
    /// Refuses to write when the Rocket League process is running.
    /// Backs up the original (once per UTC date) before any modification.
    /// </summary>
    public StatsApiConfigOutcome EnsureConfigured(RocketLeagueInstall install, StatsApiConfigDesired desired);
}
