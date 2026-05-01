namespace RocketLeagueStats.Console.HostedServices;

using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Connection;

internal sealed class StatsApiListenerService(
    IStatsApiClient client,
    IOptions<StatsApiOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<StatsApiListenerService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCancellation =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(StatsApiListenerService)),
            "Listener cancellation requested — exiting.");

    private static readonly Action<ILogger, Exception?> LogDisconnected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(StatsApiListenerService)),
            "Listener disconnected; will reconnect.");

    private static readonly Action<ILogger, int, TimeSpan, Exception?> LogRetry =
        LoggerMessage.Define<int, TimeSpan>(
            LogLevel.Warning,
            new EventId(4, nameof(StatsApiListenerService)),
            "Stats API connect failed (attempt {Attempt}). Retrying in {Delay}.");

    private static readonly Action<ILogger, int, Exception?> LogRetriesExhausted =
        LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(5, nameof(StatsApiListenerService)),
            "Stats API connect retries exhausted ({Attempts} attempts). Is Rocket League running? Requesting host shutdown.");

    private readonly StatsApiOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeline = this.BuildRetryPipeline();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pipeline.ExecuteAsync(async ct => await client.RunAsync(ct), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                LogCancellation(logger, null);
                return;
            }
            catch (SocketException ex)
            {
                // Polly exhausted its retry budget. Request a graceful host shutdown via the
                // application lifetime — re-throwing during shutdown causes deadlocks against
                // host.StopAsync, so we cooperate instead.
                LogRetriesExhausted(logger, this.options.ConnectRetry.MaxAttempts, ex);
                lifetime.StopApplication();
                return;
            }
            catch (IOException ex)
            {
                LogRetriesExhausted(logger, this.options.ConnectRetry.MaxAttempts, ex);
                lifetime.StopApplication();
                return;
            }

            // RunAsync returned (clean EOF). Honour cancellation immediately rather than re-entering
            // the pipeline if the host was already asked to stop while we were reading.
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            LogDisconnected(logger, null);
        }
    }

    private ResiliencePipeline BuildRetryPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<SocketException>().Handle<IOException>(),
                MaxRetryAttempts = this.options.ConnectRetry.MaxAttempts,
                Delay = this.options.ConnectRetry.InitialDelay,
                BackoffType = DelayBackoffType.Exponential,
                MaxDelay = this.options.ConnectRetry.MaxDelay,
                UseJitter = true,
                OnRetry = args =>
                {
                    LogRetry(logger, args.AttemptNumber, args.RetryDelay, null);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
}
