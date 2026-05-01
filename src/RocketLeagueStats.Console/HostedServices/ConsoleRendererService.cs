namespace RocketLeagueStats.Console.HostedServices;

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketLeagueStats.Console.Rendering;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Events;
using Spectre.Console;

internal sealed class ConsoleRendererService(
    StatsEventBus bus,
    IConfiguration configuration,
    ILogger<ConsoleRendererService> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(ConsoleRendererService)),
            "Console renderer started ({Mode}).");

    private readonly bool rawMode = configuration.GetValue("Console:RawMode", false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = bus.Subscribe();
        LogStarted(logger, this.rawMode ? "raw" : "default", null);

        try
        {
            await foreach (var evt in reader.ReadAllAsync(stoppingToken))
            {
                if (this.rawMode)
                {
                    AnsiConsole.WriteLine(string.Create(CultureInfo.InvariantCulture, $"<<< {evt}"));
                    continue;
                }

                if (evt is MatchStateSnapshot)
                {
                    continue;   // suppress periodic state in default mode
                }

                var line = EventFormatter.Format(evt);
                if (!string.IsNullOrEmpty(line))
                {
                    AnsiConsole.MarkupLine(line);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }
}
