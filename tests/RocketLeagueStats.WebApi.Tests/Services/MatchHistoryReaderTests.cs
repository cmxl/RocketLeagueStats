namespace RocketLeagueStats.WebApi.Tests.Services;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.Core.Persistence.Entities;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class MatchHistoryReaderTests : IAsyncLifetime, IDisposable
{
    private readonly string dbPath = Path.Combine(
        Path.GetTempPath(),
        $"rls-reader-tests-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={this.dbPath}";

    public async Task InitializeAsync()
    {
        await using var ctx = this.CreateDbContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(this.dbPath);
        TryDelete(this.dbPath + "-wal");
        TryDelete(this.dbPath + "-shm");
    }

    private StatsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<StatsDbContext>().UseSqlite(this.ConnectionString).Options);

    [Fact]
    public async Task GetMatchesAsync_returns_empty_for_pristine_database()
    {
        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetMatchesAsync(HistoryFilter.Default, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMatchesAsync_skips_unfinished_matches_and_empty_match_guids()
    {
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "completed",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
        });
        seedCtx.Matches.Add(new Match
        {
            // Still in progress — must not appear in history.
            MatchGuid = "in-progress",
            FirstSeenAtUtc = 2_000_000,
            EndedAtUtc = null,
            LastEventAtUtc = 2_100_000,
        });
        seedCtx.Matches.Add(new Match
        {
            // Empty MatchGuid (defensive — write-time filter should already prevent this, but
            // the reader filters again so historical data from before the filter is excluded too).
            MatchGuid = string.Empty,
            FirstSeenAtUtc = 3_000_000,
            EndedAtUtc = 3_300_000,
            LastEventAtUtc = 3_300_000,
        });
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetMatchesAsync(HistoryFilter.Default, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("completed", result[0].MatchId);
    }

    [Fact]
    public async Task GetMatchesAsync_aggregates_team_scores_from_persisted_goal_events_and_filters_phantoms()
    {
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "M1",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_500_000,
            LastEventAtUtc = 1_500_000,
        });

        // Two real goals (blue 2, orange 0) plus one phantom (Scorer.Name="" + GoalSpeed=0).
        AddGoal(seedCtx, "M1", 1_100_000, scorerName: "P1", scorerTeamNum: 0, goalSpeed: 1500, goalTime: 30);
        AddGoal(seedCtx, "M1", 1_200_000, scorerName: "P1", scorerTeamNum: 0, goalSpeed: 1800, goalTime: 60);
        AddGoal(seedCtx, "M1", 1_210_000, scorerName: string.Empty, scorerTeamNum: 0, goalSpeed: 0, goalTime: 0);
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetMatchesAsync(HistoryFilter.Default, CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Equal(2, summary.BlueScore);
        Assert.Equal(0, summary.OrangeScore);
        Assert.Equal(2, summary.TotalGoals);
        Assert.NotNull(summary.FastestGoal);
        Assert.Equal(1800, summary.FastestGoal!.GoalSpeedUuPerSec);
    }

    [Fact]
    public async Task GetRecapAsync_returns_null_for_unknown_match()
    {
        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetRecapAsync("does-not-exist", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRecapAsync_builds_full_recap_from_persisted_events()
    {
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "MR",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
        });

        // One real goal + one phantom (must be excluded from goal stream and from FastestGoal).
        AddGoal(seedCtx, "MR", 1_100_000, scorerName: "Tobi", scorerTeamNum: 0, goalSpeed: 1500, goalTime: 30);
        AddGoal(seedCtx, "MR", 1_115_000, scorerName: string.Empty, scorerTeamNum: 0, goalSpeed: 0, goalTime: 0);
        AddStatfeed(seedCtx, "MR", 1_200_000, statName: "EpicSave", mainName: "Tobi");
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var recap = await reader.GetRecapAsync("MR", CancellationToken.None);

        Assert.NotNull(recap);
        Assert.Equal("MR", recap!.Summary.MatchId);
        Assert.Equal(1, recap.Summary.BlueScore);
        Assert.Single(recap.Goals);
        Assert.Equal("Tobi", recap.Goals[0].Scorer.Name);
        Assert.Single(recap.Statfeeds);
        Assert.Equal(1, recap.Goals[0].BlueScoreAfter);
    }

    private static void AddGoal(
        StatsDbContext ctx,
        string matchGuid,
        long timestampUtc,
        string scorerName,
        int scorerTeamNum,
        double goalSpeed,
        double goalTime)
    {
        var goal = new GoalScoredEvent
        {
            EventName = KnownEvents.GoalScored,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampUtc),
            MatchGuid = matchGuid,
            Scorer = new PlayerRef(scorerName, scorerTeamNum + 1, scorerTeamNum),
            GoalSpeed = goalSpeed,
            GoalTime = goalTime,
            ImpactLocation = default,
        };
        ctx.Events.Add(new EventRecord
        {
            MatchGuid = matchGuid,
            EventName = KnownEvents.GoalScored,
            TimestampUtc = timestampUtc,
            Payload = JsonSerializer.Serialize<GoalScoredEvent>(goal),
        });
    }

    private static void AddStatfeed(
        StatsDbContext ctx,
        string matchGuid,
        long timestampUtc,
        string statName,
        string mainName)
    {
        var stat = new StatfeedEvent
        {
            EventName = KnownEvents.StatfeedEvent,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampUtc),
            MatchGuid = matchGuid,
            StatName = statName,
            Type = "Default",
            MainTarget = new PlayerRef(mainName, 1, 0),
        };
        ctx.Events.Add(new EventRecord
        {
            MatchGuid = matchGuid,
            EventName = KnownEvents.StatfeedEvent,
            TimestampUtc = timestampUtc,
            Payload = JsonSerializer.Serialize<StatfeedEvent>(stat),
        });
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort; temp dir gets cleaned eventually.
        }
    }
}
