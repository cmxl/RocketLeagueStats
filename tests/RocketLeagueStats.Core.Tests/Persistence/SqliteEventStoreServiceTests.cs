namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.Core.Persistence.Entities;

public sealed class SqliteEventStoreServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => this.fixture.InitializeAsync();

    public Task DisposeAsync() => this.fixture.DisposeAsync();

    public void Dispose() => this.fixture.Dispose();

    [Fact]
    public async Task WritesGoalScored_PersistsEventAndParticipants()
    {
        // The Events.MatchGuid FK is nullable but non-null values must reference an existing Match
        // row when foreign_keys=ON is set on the raw ADO.NET connection. Seed the parent row so the
        // insert path succeeds without disabling FK enforcement.
        await using (var seedCtx = this.fixture.CreateDbContext())
        {
            seedCtx.Matches.Add(new Match
            {
                MatchGuid = "match-1",
                FirstSeenAtUtc = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds(),
            });
            await seedCtx.SaveChangesAsync();
        }

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
        // Pre-seed Match row to satisfy the MatchSnapshots.MatchGuid FK; Task 14's upsert will make
        // this implicit.
        await using (var seedCtx = this.fixture.CreateDbContext())
        {
            seedCtx.Matches.Add(new Match
            {
                MatchGuid = "match-snap",
                FirstSeenAtUtc = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds(),
            });
            await seedCtx.SaveChangesAsync();
        }

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
