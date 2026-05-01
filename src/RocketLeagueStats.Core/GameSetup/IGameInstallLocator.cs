namespace RocketLeagueStats.Core.GameSetup;

public interface IGameInstallLocator
{
    /// <summary>
    /// Probes Steam libraries and Epic manifests for Rocket League. Returns null when no install is found.
    /// </summary>
    public RocketLeagueInstall? Locate();
}
