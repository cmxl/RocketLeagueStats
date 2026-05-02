namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Persistence;

public sealed class SqliteEventStoreServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => this.fixture.InitializeAsync();

    public Task DisposeAsync() => this.fixture.DisposeAsync();

    public void Dispose() => this.fixture.Dispose();

    [Fact]
    public async Task WritesGoalScored_PersistsEventAndParticipants()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // BackgroundService.StartAsync launches ExecuteAsync on a background Task and returns
        // immediately; the service calls bus.Subscribe() inside ExecuteAsync. Wait briefly so
        // the subscription is registered before we publish — otherwise the event is lost.
        await Task.Delay(100, cts.Token);

        var evt = new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1000),
            MatchGuid = "match-1",
            GoalSpeed = 100,
            GoalTime = 5,
            ImpactLocation = default,
            Scorer = new PlayerRef("Tobi", 1, 0),
            Assister = new PlayerRef("Jay", 2, 0),
        };

        bus.Publish(evt);

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 1, cts.Token);

        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        var stored = await ctx.Events.SingleAsync();
        Assert.Equal("GoalScored", stored.EventName);
        Assert.Equal("match-1", stored.MatchGuid);
        Assert.Contains("\"Scorer\"", stored.Payload);

        var participants = await ctx.EventParticipants.OrderBy(p => p.Role).ToListAsync();
        Assert.Equal(2, participants.Count);
        Assert.Equal([ParticipantRoles.Assister, ParticipantRoles.Scorer], participants.Select(p => p.Role));
        Assert.Equal(["Jay", "Tobi"], participants.Select(p => p.PlayerName));
    }

    [Fact]
    public async Task WritesUpdateState_GoesToMatchSnapshotsTable()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Same subscription-timing reason as in WritesGoalScored.
        await Task.Delay(100, cts.Token);

        using var json = System.Text.Json.JsonDocument.Parse("""{"Game":{"Teams":[{"Score":1},{"Score":2}]}}""");

        var snapshot = new MatchStateSnapshot
        {
            EventName = KnownEvents.UpdateState,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1),
            MatchGuid = "match-snap",
            RawData = json.RootElement.Clone(),
        };

        bus.Publish(snapshot);

        await WaitForRowCountAsync(this.fixture, ctx => ctx.MatchSnapshots.CountAsync(), expected: 1, cts.Token);

        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(0, await ctx.Events.CountAsync());
        var stored = await ctx.MatchSnapshots.SingleAsync();
        Assert.Equal("match-snap", stored.MatchGuid);
        Assert.Contains("\"Score\":2", stored.Payload);
    }

    [Fact]
    public async Task MatchRow_UpsertedOnFirstEvent_EnrichedOnLifecycleEvents()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 8, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Subscription-timing: see WritesGoalScored_PersistsEventAndParticipants.
        await Task.Delay(100, cts.Token);

        bus.Publish(new MatchCreatedEvent
        {
            EventName = KnownEvents.MatchCreated,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(10),
            MatchGuid = "lifecycle-1",
        });
        bus.Publish(new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(50),
            MatchGuid = "lifecycle-1",
            Scorer = new PlayerRef("Tobi", 1, 0),
            ImpactLocation = default,
        });
        bus.Publish(new MatchEndedEvent
        {
            EventName = KnownEvents.MatchEnded,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(120),
            MatchGuid = "lifecycle-1",
            WinnerTeamNum = 0,
        });

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 3, cts.Token);
        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "lifecycle-1");
        Assert.Equal(10_000L, match.CreatedAtUtc);            // ms
        Assert.Equal(120_000L, match.EndedAtUtc);
        Assert.Equal(0, match.WinnerTeamNum);
        Assert.Equal(3, match.EventCount);
        Assert.Equal(120_000L, match.LastEventAtUtc);
    }

    [Fact]
    public async Task Batching_FlushesAtMaxBatchSize()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        // High latency forces the size trigger to fire first.
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 5, MaxBatchLatencyMs = 30_000 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        for (var i = 0; i < 5; i++)
        {
            bus.Publish(new ClockUpdatedSecondsEvent
            {
                EventName = KnownEvents.ClockUpdatedSeconds,
                Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(i),
                MatchGuid = "batch-size",
                TimeSeconds = i,
            });
        }

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 5, cts.Token);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Batching_FlushesAtMaxBatchLatency()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        // Large size forces the latency trigger to fire first.
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 1000, MaxBatchLatencyMs = 100 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        bus.Publish(new ClockUpdatedSecondsEvent
        {
            EventName = KnownEvents.ClockUpdatedSeconds,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1),
            MatchGuid = "batch-latency",
            TimeSeconds = 1,
        });

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 1, cts.Token);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WritesBallHit_PersistsAllPlayersAsParticipants()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        var hit = new BallHitEvent
        {
            EventName = KnownEvents.BallHit,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(7),
            MatchGuid = "match-bh",
            Players =
            [
                new PlayerRef("Tobi", 1, 0),
                new PlayerRef("Jay", 2, 0),
                new PlayerRef("Vex", 3, 1),
            ],
            Ball = default,
        };

        bus.Publish(hit);

        await WaitForRowCountAsync(this.fixture, ctx => ctx.EventParticipants.CountAsync(), expected: 3, cts.Token);
        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        var participants = await ctx.EventParticipants.OrderBy(p => p.PlayerName).ToListAsync();
        Assert.Equal(["Jay", "Tobi", "Vex"], participants.Select(p => p.PlayerName));
        Assert.All(participants, p => Assert.Equal(ParticipantRoles.BallHit, p.Role));
    }

    private SqliteEventStoreService CreateService(StatsEventBus bus, IOptions<EventStoreOptions> options) =>
        new(
            bus,
            options,
            new EventStoreConnectionString(this.fixture.ConnectionString),
            NullLogger<SqliteEventStoreService>.Instance);

    private static async Task WaitForRowCountAsync(
        SqliteFixture fixture,
        Func<StatsDbContext, Task<int>> counter,
        int expected,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var ctx = fixture.CreateDbContext();
            var count = await counter(ctx);
            if (count >= expected)
            {
                return;
            }

            await Task.Delay(25, cancellationToken);
        }

        throw new TimeoutException($"Expected at least {expected} rows but timed out.");
    }
}
