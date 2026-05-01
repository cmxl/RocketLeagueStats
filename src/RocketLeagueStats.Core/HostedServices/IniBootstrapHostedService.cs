namespace RocketLeagueStats.Core.HostedServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.GameSetup;

internal sealed class IniBootstrapHostedService(
    IOptions<GameSetupOptions> gameSetupOptions,
    IOptions<StatsApiOptions> statsOptions,
    IGameInstallLocator locator,
    IStatsApiConfigWriter writer,
    ILogger<IniBootstrapHostedService> logger) : IHostedService
{
    private static readonly Action<ILogger, Exception?> LogAutoConfigDisabled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(IniBootstrapHostedService)),
            "Auto-configure of DefaultStatsAPI.ini is disabled.");

    private static readonly Action<ILogger, Exception?> LogLocating =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(IniBootstrapHostedService)),
            "Locating Rocket League installation...");

    private static readonly Action<ILogger, int, int, Exception?> LogInstallNotFound =
        LoggerMessage.Define<int, int>(
            LogLevel.Warning,
            new EventId(3, nameof(IniBootstrapHostedService)),
            "Rocket League installation not detected. Configure DefaultStatsAPI.ini manually with PacketSendRate={Rate}, Port={Port}.");

    private static readonly Action<ILogger, RocketLeagueInstallSource, string, Exception?> LogInstallFound =
        LoggerMessage.Define<RocketLeagueInstallSource, string>(
            LogLevel.Information,
            new EventId(4, nameof(IniBootstrapHostedService)),
            "Found {Source} install at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogConfigUpdated =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(5, nameof(IniBootstrapHostedService)),
            "Stats API config updated: {Keys}");

    private static readonly Action<ILogger, string, Exception?> LogConfigUnchanged =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(6, nameof(IniBootstrapHostedService)),
            "Stats API config unchanged: {Reason}");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var setup = gameSetupOptions.Value;
        if (!setup.AutoConfigureIni)
        {
            LogAutoConfigDisabled(logger, null);
            return Task.CompletedTask;
        }

        LogLocating(logger, null);
        var install = locator.Locate();
        if (install is null)
        {
            LogInstallNotFound(logger, setup.PacketSendRate, statsOptions.Value.Port, null);
            return Task.CompletedTask;
        }

        LogInstallFound(logger, install.Source, install.Path, null);

        var outcome = writer.EnsureConfigured(
            install,
            new StatsApiConfigDesired(setup.PacketSendRate, statsOptions.Value.Port));

        if (outcome.Changed)
        {
            LogConfigUpdated(logger, string.Join(", ", outcome.ChangedKeys), null);
        }
        else
        {
            LogConfigUnchanged(logger, outcome.Reason, null);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
