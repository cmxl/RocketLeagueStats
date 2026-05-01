using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RocketLeagueStats.Core.Tests.Connection;

/// <summary>
/// Minimal TCP server that emits a scripted sequence of newline-terminated lines
/// to the first client that connects, then closes the connection.
/// </summary>
internal sealed class FakeStatsApiServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly IReadOnlyList<string> scriptLines;
    private readonly TimeSpan delayBetweenLines;
    private readonly string lineTerminator;
    private Task? serverTask;
    private readonly CancellationTokenSource cts = new();

    public int Port { get; }

    private FakeStatsApiServer(IReadOnlyList<string> scriptLines, TimeSpan delayBetweenLines, string lineTerminator)
    {
        this.scriptLines = scriptLines;
        this.delayBetweenLines = delayBetweenLines;
        this.lineTerminator = lineTerminator;
        this.listener = new TcpListener(IPAddress.Loopback, 0);
        this.listener.Start();
        this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
    }

    public static FakeStatsApiServer Start(
        IReadOnlyList<string> scriptLines,
        TimeSpan? delayBetweenLines = null,
        string lineTerminator = "\n")
    {
        var server = new FakeStatsApiServer(scriptLines, delayBetweenLines ?? TimeSpan.Zero, lineTerminator);
        server.serverTask = server.ServeOnceAsync();
        return server;
    }

    private async Task ServeOnceAsync()
    {
        try
        {
            using var client = await this.listener.AcceptTcpClientAsync(this.cts.Token);
            client.NoDelay = true;   // disable Nagle so each WriteAsync round-trips on its own
            await using var stream = client.GetStream();
            foreach (var line in this.scriptLines)
            {
                if (this.cts.IsCancellationRequested)
                {
                    break;
                }

                var bytes = Encoding.UTF8.GetBytes(line + this.lineTerminator);
                await stream.WriteAsync(bytes, this.cts.Token);
                await stream.FlushAsync(this.cts.Token);
                if (this.delayBetweenLines > TimeSpan.Zero)
                {
                    await Task.Delay(this.delayBetweenLines, this.cts.Token);
                }
            }
        }
        catch (OperationCanceledException) { /* expected during shutdown */ }
        catch (IOException) { /* client may close mid-write — fine */ }
        catch (SocketException) { /* expected during shutdown */ }
    }

    public async ValueTask DisposeAsync()
    {
        this.cts.Cancel();
        this.listener.Stop();
        if (this.serverTask is not null)
        {
            try
            {
                await this.serverTask;
            }
            catch
            {
                /* drained above */
            }
        }

        this.cts.Dispose();
    }
}
