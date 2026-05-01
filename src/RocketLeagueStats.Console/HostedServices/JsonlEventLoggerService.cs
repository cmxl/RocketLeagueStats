namespace RocketLeagueStats.Console.HostedServices;

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;

internal sealed class JsonlEventLoggerService(
    StatsEventBus bus,
    IOptions<EventLogOptions> options,
    ILogger<JsonlEventLoggerService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDisabled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(JsonlEventLoggerService)),
            "Event log disabled.");

    private static readonly Action<ILogger, string, Exception?> LogStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(JsonlEventLoggerService)),
            "JSONL event logger started — directory: {Dir}");

    private static readonly Action<ILogger, Exception?> LogWriteFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(JsonlEventLoggerService)),
            "Failed to write event to JSONL log; dropping event.");

    private static readonly Action<ILogger, string, Exception?> LogRotated =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, nameof(JsonlEventLoggerService)),
            "Rotated event log to {GzPath}");

    private static readonly Action<ILogger, string, Exception?> LogRotationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5, nameof(JsonlEventLoggerService)),
            "Rotation failed for {Path}; leaving uncompressed.");

    private static readonly Action<ILogger, string, Exception?> LogDeletedAged =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(6, nameof(JsonlEventLoggerService)),
            "Deleted aged log: {File}");

    private static readonly Action<ILogger, string, Exception?> LogDeleteFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(7, nameof(JsonlEventLoggerService)),
            "Failed to delete {File}");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly EventLogOptions options = options.Value;

    private string? currentFilePath;
    private DateTime currentFileDateUtc;
    private long currentFileBytesWritten;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            LogDisabled(logger, null);
            await base.StartAsync(cancellationToken);
            return;
        }

        var resolvedDir = this.ResolveDirectory();
        Directory.CreateDirectory(resolvedDir);
        this.CleanupOldArchives(resolvedDir);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var dir = this.ResolveDirectory();
        var reader = bus.Subscribe();
        LogStarted(logger, dir, null);

        try
        {
            await foreach (var evt in reader.ReadAllAsync(stoppingToken))
            {
                if (evt is MatchStateSnapshot)
                {
                    continue;   // periodic state not persisted (Section 6.6)
                }

                try
                {
                    await this.WriteEventAsync(dir, evt, stoppingToken);
                }
                catch (IOException ex)
                {
                    LogWriteFailed(logger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }

    private async Task WriteEventAsync(string dir, StatsEvent evt, CancellationToken cancellationToken)
    {
        await this.EnsureCurrentFileAsync(dir, cancellationToken);

        var json = JsonSerializer.Serialize(evt, evt.GetType(), SerializerOptions);
        var line = json + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(line);

        await File.AppendAllTextAsync(this.currentFilePath!, line, Encoding.UTF8, cancellationToken);
        this.currentFileBytesWritten += bytes.LongLength;

        if (this.currentFileBytesWritten >= this.options.MaxFileSizeBytes)
        {
            await this.RotateAsync(dir, sizeTriggered: true, cancellationToken);
        }
    }

    private async Task EnsureCurrentFileAsync(string dir, CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        if (this.currentFilePath is not null && this.currentFileDateUtc == todayUtc)
        {
            return;
        }

        if (this.currentFilePath is not null)
        {
            await this.RotateAsync(dir, sizeTriggered: false, cancellationToken);
        }

        this.currentFilePath = Path.Combine(dir, $"rl-stats-{todayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.jsonl");
        this.currentFileDateUtc = todayUtc;
        this.currentFileBytesWritten = File.Exists(this.currentFilePath) ? new FileInfo(this.currentFilePath).Length : 0;
    }

    private async Task RotateAsync(string dir, bool sizeTriggered, CancellationToken cancellationToken)
    {
        if (this.currentFilePath is null || !File.Exists(this.currentFilePath))
        {
            this.currentFilePath = null;
            return;
        }

        try
        {
            var datePart = this.currentFileDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var gzPath = sizeTriggered
                ? Path.Combine(dir, $"rl-stats-{datePart}-{NextSequence(dir, this.currentFileDateUtc).ToString("D3", CultureInfo.InvariantCulture)}.jsonl.gz")
                : Path.Combine(dir, $"rl-stats-{datePart}.jsonl.gz");

            await using (var input = File.OpenRead(this.currentFilePath))
            await using (var output = File.Create(gzPath))
            await using (var gz = new GZipStream(output, CompressionLevel.Optimal))
            {
                await input.CopyToAsync(gz, cancellationToken);
            }

            File.Delete(this.currentFilePath);
            LogRotated(logger, gzPath, null);
        }
        catch (IOException ex)
        {
            LogRotationFailed(logger, this.currentFilePath, ex);
        }
        finally
        {
            this.currentFilePath = null;
            this.currentFileBytesWritten = 0;
        }
    }

    private static int NextSequence(string dir, DateTime dateUtc)
    {
        var prefix = $"rl-stats-{dateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}-";
        var existing = Directory.EnumerateFiles(dir, $"{prefix}*.jsonl.gz")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(n => n![prefix.Length..].Split('.')[0])
            .Select(n => int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0)
            .DefaultIfEmpty(0)
            .Max();
        return existing + 1;
    }

    private void CleanupOldArchives(string dir)
    {
        var threshold = DateTime.UtcNow.AddDays(-this.options.RetentionDays);
        foreach (var file in Directory.EnumerateFiles(dir, "rl-stats-*.jsonl.gz"))
        {
            try
            {
                if (File.GetCreationTimeUtc(file) < threshold)
                {
                    File.Delete(file);
                    LogDeletedAged(logger, file, null);
                }
            }
            catch (IOException ex)
            {
                LogDeleteFailed(logger, file, ex);
            }
        }
    }

    private string ResolveDirectory()
    {
        var configured = this.options.Directory;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "logs")
            : Environment.ExpandEnvironmentVariables(configured);
    }
}
