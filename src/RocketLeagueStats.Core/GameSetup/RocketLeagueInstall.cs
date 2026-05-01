namespace RocketLeagueStats.Core.GameSetup;

public sealed record RocketLeagueInstall(string Path, RocketLeagueInstallSource Source);

public enum RocketLeagueInstallSource
{
    Steam,
    Epic
}
