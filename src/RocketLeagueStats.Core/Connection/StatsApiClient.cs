namespace RocketLeagueStats.Core.Connection;

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;

public sealed class StatsApiClient(
    IOptions<StatsApiOptions> options,
    StatsEventBus bus,
    ILogger<StatsApiClient> logger) : IStatsApiClient
{
    private const int TraceBufferSize = 8192;
    private const int TracePreviewBytes = 256;
    private const int FramerInitialBuffer = 16 * 1024;
    private const int FramerMaxBuffer = 256 * 1024;

    private static readonly Action<ILogger, int, Exception?> LogConnected =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(StatsApiClient)),
            "Connected to Stats API at 127.0.0.1:{Port}.");

    private static readonly Action<ILogger, string, Exception?> LogMalformedJson =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(StatsApiClient)),
            "Skipping malformed JSON line: {Snippet}");

    private static readonly Action<ILogger, string, Exception?> LogParseFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(StatsApiClient)),
            "Skipping line that caused parse failure: {Snippet}");

    private static readonly Action<ILogger, Exception?> LogStreamEnded =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4, nameof(StatsApiClient)),
            "Stats API stream ended.");

    private static readonly Action<ILogger, Exception?> LogTraceStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(5, nameof(StatsApiClient)),
            "Trace mode enabled — dumping raw socket bytes; events will not be published to the bus.");

    private static readonly Action<ILogger, int, int, string, string, Exception?> LogTraceChunk =
        LoggerMessage.Define<int, int, string, string>(
            LogLevel.Information,
            new EventId(6, nameof(StatsApiClient)),
            "Trace chunk #{Index}: {Length} bytes  hex: {Hex}  utf8: {Utf8}");

    private static readonly Action<ILogger, int, Exception?> LogFramerOverflow =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(7, nameof(StatsApiClient)),
            "Object framer buffer hit maximum size ({Bytes} B) without finding a complete object — discarding and resyncing.");

    private readonly StatsApiOptions options = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, this.options.Port, cancellationToken);
        LogConnected(logger, this.options.Port, null);

        await using var stream = tcp.GetStream();

        if (this.options.TraceMode)
        {
            await this.RunTraceLoopAsync(stream, cancellationToken);
            return;
        }

        await this.RunFramedLoopAsync(stream, cancellationToken);
    }

    private async Task RunFramedLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        // Each TCP message from the Stats API plugin is one complete JSON object with no terminator.
        // We read into a pooled byte buffer, then use brace-depth tracking (string-aware) to slice out
        // each complete top-level object and hand it to the parser as a ReadOnlyMemory<byte>.
        var buffer = ArrayPool<byte>.Shared.Rent(FramerInitialBuffer);
        var filled = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (filled == buffer.Length)
                {
                    if (buffer.Length >= FramerMaxBuffer)
                    {
                        LogFramerOverflow(logger, buffer.Length, null);
                        filled = 0;
                        continue;
                    }

                    buffer = GrowBuffer(buffer, filled);
                }

                int read;
                try
                {
                    read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (read == 0)
                {
                    break;   // EOF — peer closed
                }

                filled += read;
                filled = this.DrainObjects(buffer, filled);
            }

            LogStreamEnded(logger, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private int DrainObjects(byte[] buffer, int filled)
    {
        var cursor = 0;
        while (JsonObjectFramer.TryFind(buffer.AsSpan(cursor, filled - cursor), out var consumed, out var objStart, out var objLength))
        {
            // objStart is relative to the slice (cursor); translate back to absolute buffer index.
            var absoluteStart = cursor + objStart;
            this.HandleObject(buffer.AsMemory(absoluteStart, objLength));
            cursor += consumed;
        }

        if (cursor == 0)
        {
            return filled;
        }

        var remaining = filled - cursor;
        if (remaining > 0)
        {
            Buffer.BlockCopy(buffer, cursor, buffer, 0, remaining);
        }

        return remaining;
    }

    private void HandleObject(ReadOnlyMemory<byte> objectBytes)
    {
        try
        {
            var evt = StatsEventParser.Parse(objectBytes);
            bus.Publish(evt);
        }
        catch (JsonException ex)
        {
            LogMalformedJson(logger, MakeSnippet(objectBytes), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogParseFailed(logger, MakeSnippet(objectBytes), ex);
        }
    }

    private static byte[] GrowBuffer(byte[] current, int filled)
    {
        var newSize = Math.Min(current.Length * 2, FramerMaxBuffer);
        var grown = ArrayPool<byte>.Shared.Rent(newSize);
        Buffer.BlockCopy(current, 0, grown, 0, filled);
        ArrayPool<byte>.Shared.Return(current);
        return grown;
    }

    private const int SnippetMaxBytes = 500;

    private static string MakeSnippet(ReadOnlyMemory<byte> bytes)
    {
        var len = Math.Min(bytes.Length, SnippetMaxBytes);
        var s = Encoding.UTF8.GetString(bytes.Span[..len]);
        return bytes.Length > SnippetMaxBytes ? s + "..." : s;
    }

    private async Task RunTraceLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        LogTraceStarted(logger, null);
        var buffer = new byte[TraceBufferSize];
        var chunkIndex = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                LogStreamEnded(logger, null);
                return;
            }

            var hex = ToHexPreview(buffer, read, TracePreviewBytes);
            var utf8 = ToUtf8Preview(buffer, read, TracePreviewBytes);
            LogTraceChunk(logger, chunkIndex, read, hex, utf8, null);
            chunkIndex++;
        }
    }

    private static string ToHexPreview(byte[] buffer, int length, int maxBytes)
    {
        var n = Math.Min(length, maxBytes);
        var sb = new StringBuilder((n * 3) + 3);
        for (var i = 0; i < n; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(buffer[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (length > maxBytes)
        {
            sb.Append("...");
        }

        return sb.ToString();
    }

    private static string ToUtf8Preview(byte[] buffer, int length, int maxBytes)
    {
        var n = Math.Min(length, maxBytes);
        var decoded = Encoding.UTF8.GetString(buffer, 0, n);
        var sb = new StringBuilder(decoded.Length + 3);
        foreach (var c in decoded)
        {
            if (c == '\r')
            {
                sb.Append("\\r");
            }
            else if (c == '\n')
            {
                sb.Append("\\n");
            }
            else if (c == '\t')
            {
                sb.Append("\\t");
            }
            else if (c is >= ' ' and < (char)0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('.');
            }
        }

        if (length > maxBytes)
        {
            sb.Append("...");
        }

        return sb.ToString();
    }
}
