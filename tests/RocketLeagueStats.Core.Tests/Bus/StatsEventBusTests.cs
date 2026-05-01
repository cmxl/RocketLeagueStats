using Microsoft.Extensions.Logging.Abstractions;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Events;

namespace RocketLeagueStats.Core.Tests.Bus;

public class StatsEventBusTests
{
    private static GoalScoredEvent SampleEvent(string name = KnownEvents.GoalScored) =>
        new()
        {
            EventName = name,
            Scorer = new PlayerRef("P", 1, 0),
            GoalSpeed = 1500,
        };

    [Fact]
    public async Task Single_subscriber_receives_published_event()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();

        bus.Publish(SampleEvent());

        var evt = await reader.ReadAsync(CancellationToken.None);
        Assert.Equal(KnownEvents.GoalScored, evt.EventName);
    }

    [Fact]
    public async Task Multiple_subscribers_each_receive_every_event_independently()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var a = bus.Subscribe();
        var b = bus.Subscribe();

        bus.Publish(SampleEvent("A"));
        bus.Publish(SampleEvent("B"));

        var evtA1 = await a.ReadAsync(CancellationToken.None);
        var evtA2 = await a.ReadAsync(CancellationToken.None);
        var evtB1 = await b.ReadAsync(CancellationToken.None);
        var evtB2 = await b.ReadAsync(CancellationToken.None);

        Assert.Equal(["A", "B"], [evtA1.EventName, evtA2.EventName]);
        Assert.Equal(["A", "B"], [evtB1.EventName, evtB2.EventName]);
    }

    [Fact]
    public async Task Slow_subscriber_drops_oldest_without_affecting_others()
    {
        // Capacity=2: fast drains as events arrive; slow never reads, so it drops.
        using var bus = new StatsEventBus(
            NullLogger<StatsEventBus>.Instance,
            new StatsEventBusOptions { Capacity = 2 });
        var slow = bus.Subscribe();
        var fast = bus.Subscribe();

        bus.Publish(SampleEvent("1"));
        // Fast drains "1" immediately, keeping its channel empty before the next publish.
        var f1 = await fast.ReadAsync(CancellationToken.None);

        bus.Publish(SampleEvent("2"));
        // Fast drains "2", still empty before the next publish.
        var f2 = await fast.ReadAsync(CancellationToken.None);

        // slow's channel is now full (["1","2"]); this publish drops "1" for slow.
        bus.Publish(SampleEvent("3"));
        var f3 = await fast.ReadAsync(CancellationToken.None);

        // Fast received all three events in order.
        Assert.Equal(["1", "2", "3"], [f1.EventName, f2.EventName, f3.EventName]);

        // Slow subscriber sees only the newest two (oldest "1" was dropped).
        var s1 = await slow.ReadAsync(CancellationToken.None);
        var s2 = await slow.ReadAsync(CancellationToken.None);
        Assert.Equal(["2", "3"], [s1.EventName, s2.EventName]);
    }

    [Fact]
    public void Disposing_bus_completes_all_subscriber_channels()
    {
        var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();

        bus.Dispose();

        Assert.True(reader.Completion.IsCompleted);
    }
}
