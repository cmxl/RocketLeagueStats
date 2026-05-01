namespace RocketLeagueStats.Core.HostedServices;

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;

/// <summary>
/// Writes the first <c>MatchStateSnapshot</c> of each match as raw JSON to disk. Off by
/// default; enable via <c>Diagnostics:DumpSnapshots=true</c> (or <c>--dump-snapshot</c>).
/// Used to discover the wire-format shape so the projector can extract playlist + roster.
/// </summary>
internal sealed class SnapshotDumperService(
    StatsEventBus bus,
    IOptions<DiagnosticsOptions> options,
    ILogger<SnapshotDumperService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDisabled =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1, nameof(SnapshotDumperService)),
            "Snapshot dumper disabled (Diagnostics:DumpSnapshots=false).");

    private static readonly Action<ILogger, string, Exception?> LogStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(SnapshotDumperService)),
            "Snapshot dumper enabled — writing first snapshot per match to {Dir}");

    private static readonly Action<ILogger, string, Exception?> LogDumped =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, nameof(SnapshotDumperService)),
            "Dumped first MatchStateSnapshot to {Path}");

    private static readonly Action<ILogger, Exception?> LogWriteFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(4, nameof(SnapshotDumperService)),
            "Failed to write snapshot dump.");

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly DiagnosticsOptions options = options.Value;
    private bool dumpedForCurrentMatch;
    private int matchIndex;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.DumpSnapshots)
        {
            LogDisabled(logger, null);
            return;
        }

        var dir = this.ResolveDirectory();
        Directory.CreateDirectory(dir);
        LogStarted(logger, dir, null);

        var reader = bus.Subscribe();
        try
        {
            await foreach (var evt in reader.ReadAllAsync(stoppingToken))
            {
                switch (evt)
                {
                    case MatchInitializedEvent:
                        this.dumpedForCurrentMatch = false;
                        this.matchIndex++;
                        break;
                    case MatchEndedEvent:
                    case MatchDestroyedEvent:
                        this.dumpedForCurrentMatch = false;
                        break;
                    case MatchStateSnapshot snap when !this.dumpedForCurrentMatch:
                        await this.WriteSnapshotAsync(dir, snap, stoppingToken);
                        this.dumpedForCurrentMatch = true;
                        break;
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task WriteSnapshotAsync(string dir, MatchStateSnapshot snap, CancellationToken ct)
    {
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var seq = this.matchIndex.ToString("D3", CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, $"snapshot-{stamp}-match{seq}.json");

            await using var fs = File.Create(path);
            await using var writer = new Utf8JsonWriter(fs, WriterOptions);
            snap.RawData.WriteTo(writer);
            await writer.FlushAsync(ct);
            LogDumped(logger, path, null);
        }
        catch (IOException ex)
        {
            LogWriteFailed(logger, ex);
        }
    }

    private string ResolveDirectory()
    {
        var configured = this.options.Directory;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(System.IO.Directory.GetCurrentDirectory(), "logs", "snapshots")
            : Environment.ExpandEnvironmentVariables(configured);
    }
}
