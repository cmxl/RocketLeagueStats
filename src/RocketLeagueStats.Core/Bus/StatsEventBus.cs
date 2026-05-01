namespace RocketLeagueStats.Core.Bus;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RocketLeagueStats.Core.Events;

public sealed class StatsEventBusOptions
{
    public int Capacity { get; init; } = 10_000;

    public TimeSpan DropWarningCoalesceWindow { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class StatsEventBus : IDisposable
{
    private static readonly Action<ILogger, int, long, TimeSpan, Exception?> LogDropWarning =
        LoggerMessage.Define<int, long, TimeSpan>(
            LogLevel.Warning,
            new EventId(1, nameof(StatsEventBus)),
            "Subscriber {SubscriberId} dropped {DropCount} events in the last {Window}.");

    private readonly ILogger<StatsEventBus> logger;
    private readonly StatsEventBusOptions options;
    private readonly ConcurrentDictionary<int, Subscriber> subscribers = new();
    private readonly Timer dropReporter;
    private int nextId;
    private bool disposed;

    public StatsEventBus(ILogger<StatsEventBus> logger, StatsEventBusOptions? options = null)
    {
        this.logger = logger;
        this.options = options ?? new StatsEventBusOptions();
        this.dropReporter = new Timer(
            this.ReportDrops,
            null,
            this.options.DropWarningCoalesceWindow,
            this.options.DropWarningCoalesceWindow);
    }

    public ChannelReader<StatsEvent> Subscribe()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        var id = Interlocked.Increment(ref this.nextId);
        var channel = Channel.CreateBounded<StatsEvent>(new BoundedChannelOptions(this.options.Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        var subscriber = new Subscriber(id, channel, new SubscriberDropTracker(id));
        this.subscribers.TryAdd(id, subscriber);
        return channel.Reader;
    }

    public void Publish(StatsEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        foreach (var sub in this.subscribers.Values)
        {
            // BoundedChannelFullMode.DropOldest means TryWrite always succeeds and
            // silently drops the oldest item when full. We can't observe drops via
            // TryWrite; instead, we sample channel count before write to detect them.
            var beforeCount = sub.Channel.Reader.Count;
            sub.Channel.Writer.TryWrite(evt);
            var afterCount = sub.Channel.Reader.Count;

            // Drop occurred when the buffer was at capacity AND the count did not grow.
            if ((beforeCount >= this.options.Capacity) && (afterCount == beforeCount))
            {
                sub.DropTracker.Increment();
            }
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.dropReporter.Dispose();
        foreach (var sub in this.subscribers.Values)
        {
            sub.Channel.Writer.TryComplete();
        }

        this.subscribers.Clear();
    }

    private void ReportDrops(object? state)
    {
        foreach (var sub in this.subscribers.Values)
        {
            var drops = sub.DropTracker.Snapshot();
            if (drops > 0)
            {
                LogDropWarning(this.logger, sub.Id, drops, this.options.DropWarningCoalesceWindow, null);
            }
        }
    }

    private sealed record Subscriber(int Id, Channel<StatsEvent> Channel, SubscriberDropTracker DropTracker);
}
