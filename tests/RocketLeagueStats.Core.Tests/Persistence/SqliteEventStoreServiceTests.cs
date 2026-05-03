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
    public async Task ServiceSurvivesSustainedBurst_NoEventsLost()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 5, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        const int total = 50;
        for (var i = 0; i < total; i++)
        {
            bus.Publish(new GoalScoredEvent
            {
                EventName = KnownEvents.GoalScored,
                Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1 + i),
                MatchGuid = "burst",
                Scorer = new PlayerRef("Tobi", 1, 0),
                ImpactLocation = default,
            });
        }

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: total, cts.Token);
        await service.StopAsync(CancellationToken.None);

        Assert.False(service.ExecuteTask?.IsFaulted ?? false);

        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(total, await ctx.Events.CountAsync());
    }

    [Fact]
    public async Task Idempotency_ReplayingSameLogicalEventsIsSafe()
    {
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        for (var i = 0; i < 3; i++)
        {
            bus.Publish(new GoalScoredEvent
            {
                EventName = KnownEvents.GoalScored,
                Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1 + i),
                MatchGuid = "idempotent",
                Scorer = new PlayerRef("Tobi", 1, 0),
                ImpactLocation = default,
            });
        }

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 3, cts.Token);
        await service.StopAsync(CancellationToken.None);

        // Each event has its own auto-incremented Id so participant PKs (EventId, PlayerName, Role) never collide.
        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(3, await ctx.Events.CountAsync());
        Assert.Equal(3, await ctx.EventParticipants.CountAsync(p => p.PlayerName == "Tobi" && p.Role == ParticipantRoles.Scorer));
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

    [Fact]
    public async Task EventsWithEmptyMatchGuid_AreDropped_NoRowsInserted()
    {
        // Training / free-play / private-match events arrive with an empty MatchGuid. The store
        // skips them at the bus-read step so the schema stays free of orphan rows that would
        // never have a parent Match. A subsequent online event in the same session is unaffected.
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);

        // Empty-string MatchGuid (training/freeplay shape on the wire).
        bus.Publish(new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1),
            MatchGuid = string.Empty,
            Scorer = new PlayerRef("Tobi", 1, 0),
            ImpactLocation = default,
        });

        // Null MatchGuid (defensive — shouldn't happen on the wire for typed events but the
        // type system allows it via StatsEvent.MatchGuid being string?).
        bus.Publish(new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(2),
            MatchGuid = null,
            Scorer = new PlayerRef("Jay", 2, 0),
            ImpactLocation = default,
        });

        // A real online event should still land — proves we filter on the event, not the session.
        bus.Publish(new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(3),
            MatchGuid = "real-match",
            Scorer = new PlayerRef("Vex", 3, 0),
            ImpactLocation = default,
        });

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Events.CountAsync(), expected: 1, cts.Token);
        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(1, await ctx.Events.CountAsync());
        Assert.Equal(1, await ctx.Matches.CountAsync());
        var stored = await ctx.Events.SingleAsync();
        Assert.Equal("real-match", stored.MatchGuid);
    }

    [Fact]
    public async Task MatchEnded_WithPriorSnapshot_PersistsTeamMetadataAndPlayerMatchStats()
    {
        // Wire path: snapshot ticks at ~30Hz throughout the match → MatchEnded fires once at the end.
        // The writer must capture the latest snapshot's team metadata + per-player stats and persist
        // them with the Match row + a PlayerMatchStats row per player at MatchEnded time.
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 8, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        using var snapshotJson = System.Text.Json.JsonDocument.Parse("""
            {
              "MatchGuid": "ended-with-meta",
              "Players": [
                { "Name": "Tobi", "PrimaryId": "Steam|76561198|0", "Shortcut": 1, "TeamNum": 0,
                  "Score": 540, "Goals": 3, "Assists": 2, "Saves": 4, "Shots": 7, "Touches": 42 },
                { "Name": "Vex", "PrimaryId": "Epic|abc123|0", "Shortcut": 2, "TeamNum": 1,
                  "Score": 320, "Goals": 1, "Assists": 1, "Saves": 2, "Shots": 4, "Touches": 28 }
              ],
              "Game": {
                "Arena": "DFH Stadium",
                "Teams": [
                  { "Name": "BLUE", "TeamNum": 0, "ColorPrimary": "1873FF", "ColorSecondary": "0F3D8A" },
                  { "Name": "ORANGE", "TeamNum": 1, "ColorPrimary": "F08020", "ColorSecondary": "8A4015" }
                ]
              }
            }
            """);

        bus.Publish(new MatchInitializedEvent
        {
            EventName = KnownEvents.MatchInitialized,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(10),
            MatchGuid = "ended-with-meta",
        });
        bus.Publish(new MatchStateSnapshot
        {
            EventName = KnownEvents.UpdateState,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(60),
            MatchGuid = "ended-with-meta",
            RawData = snapshotJson.RootElement.Clone(),
        });
        bus.Publish(new MatchEndedEvent
        {
            EventName = KnownEvents.MatchEnded,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(120),
            MatchGuid = "ended-with-meta",
            WinnerTeamNum = 0,
        });

        await WaitForRowCountAsync(this.fixture, ctx => ctx.PlayerMatchStats.CountAsync(), expected: 2, cts.Token);
        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "ended-with-meta");
        Assert.Equal("BLUE", match.BlueTeamName);
        Assert.Equal("1873FF", match.BlueColorPrimary);
        Assert.Equal("ORANGE", match.OrangeTeamName);
        Assert.Equal("F08020", match.OrangeColorPrimary);
        Assert.Equal("DFH Stadium", match.Arena);

        var statsByName = await ctx.PlayerMatchStats
            .Where(p => p.MatchGuid == "ended-with-meta")
            .ToDictionaryAsync(p => p.PlayerName, cts.Token);
        Assert.Equal(2, statsByName.Count);
        Assert.Equal(540, statsByName["Tobi"].Score);
        Assert.Equal("Steam", statsByName["Tobi"].Platform);
        Assert.Equal(3, statsByName["Tobi"].Goals);
        Assert.Equal(42, statsByName["Tobi"].Touches);
        Assert.Equal("Epic", statsByName["Vex"].Platform);
        Assert.Equal(320, statsByName["Vex"].Score);
    }

    [Fact]
    public async Task MatchEnded_WithoutAnySnapshot_PersistsMatchWithoutTeamMetadata()
    {
        // Edge case: a match ends before any snapshot has been seen for it (e.g., wire blip).
        // The writer must still persist the Match row — just without team metadata or
        // PlayerMatchStats — instead of crashing or skipping the match entirely.
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var options = Options.Create(new EventStoreOptions { MaxBatchSize = 4, MaxBatchLatencyMs = 50 });
        var service = this.CreateService(bus, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        bus.Publish(new MatchInitializedEvent
        {
            EventName = KnownEvents.MatchInitialized,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(10),
            MatchGuid = "ended-no-snap",
        });
        bus.Publish(new MatchEndedEvent
        {
            EventName = KnownEvents.MatchEnded,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(120),
            MatchGuid = "ended-no-snap",
            WinnerTeamNum = 0,
        });

        await WaitForRowCountAsync(this.fixture, ctx => ctx.Matches.CountAsync(m => m.MatchGuid == "ended-no-snap"), expected: 1, cts.Token);
        await Task.Delay(200, cts.Token);   // Let the latency-flush settle in case PlayerMatchStats inserts trail Matches.
        await service.StopAsync(CancellationToken.None);

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "ended-no-snap");
        Assert.Null(match.BlueTeamName);
        Assert.Null(match.OrangeTeamName);
        Assert.Null(match.Arena);
        Assert.Equal(120_000L, match.EndedAtUtc);
        Assert.Equal(0, await ctx.PlayerMatchStats.CountAsync(p => p.MatchGuid == "ended-no-snap"));
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
