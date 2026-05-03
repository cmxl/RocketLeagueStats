namespace RocketLeagueStats.Core.HostedServices;

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

    private static readonly Action<ILogger, int, Exception?> LogPipelineExhausted =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(5, nameof(StatsApiListenerService)),
            "Stats API connect retries exhausted ({Attempts} attempts) — re-entering pipeline. The WebApi keeps running so you can still browse history while Rocket League is closed.");

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
                // Defensive — with the default MaxAttempts = int.MaxValue this catch is effectively
                // unreachable, but smaller values (test fixtures, custom config) can still exhaust
                // the pipeline. Sleep one MaxDelay then re-enter the outer loop instead of letting
                // the BackgroundService die — the WebApi must stay up so users can browse history /
                // recap UI even with the game closed.
                LogPipelineExhausted(logger, this.options.ConnectRetry.MaxAttempts, ex);
                await SafeDelayAsync(this.options.ConnectRetry.MaxDelay, stoppingToken);
            }
            catch (IOException ex)
            {
                LogPipelineExhausted(logger, this.options.ConnectRetry.MaxAttempts, ex);
                await SafeDelayAsync(this.options.ConnectRetry.MaxDelay, stoppingToken);
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

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during the cooldown is fine — outer loop re-checks the token next pass.
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
