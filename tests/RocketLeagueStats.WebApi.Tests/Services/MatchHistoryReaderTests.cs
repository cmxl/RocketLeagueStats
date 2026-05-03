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

    [Fact]
    public async Task GetMatchesAsync_returns_team_metadata_and_arena_from_persisted_columns()
    {
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "MT-WITH-META",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
            BlueTeamName = "BLUE",
            BlueColorPrimary = "1873FF",
            BlueColorSecondary = "0F3D8A",
            OrangeTeamName = "ORANGE",
            OrangeColorPrimary = "F08020",
            OrangeColorSecondary = "8A4015",
            Arena = "DFH Stadium",
        });
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetMatchesAsync(HistoryFilter.Default, CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.NotNull(summary.BlueTeam);
        Assert.Equal("BLUE", summary.BlueTeam!.Name);
        Assert.Equal("1873FF", summary.BlueTeam.ColorPrimary);
        Assert.Equal("0F3D8A", summary.BlueTeam.ColorSecondary);
        Assert.NotNull(summary.OrangeTeam);
        Assert.Equal("F08020", summary.OrangeTeam!.ColorPrimary);
        Assert.Equal("DFH Stadium", summary.ArenaName);
    }

    [Fact]
    public async Task GetMatchesAsync_returns_null_team_metadata_for_legacy_rows_without_columns()
    {
        // Pre-migration AddTeamMetadataAndPlayerStats rows have all the new columns NULL — the
        // reader must surface that as null TeamDto so the frontend falls back to its default
        // palette instead of rendering empty-string color names.
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "MT-LEGACY",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
            // No team metadata set — simulates a row written before migration landed.
        });
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var result = await reader.GetMatchesAsync(HistoryFilter.Default, CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Null(summary.BlueTeam);
        Assert.Null(summary.OrangeTeam);
        Assert.Null(summary.ArenaName);
    }

    [Fact]
    public async Task GetRecapAsync_overlays_persisted_player_stats_and_surfaces_platform()
    {
        // PlayerMatchStats persists the wire's authoritative scoreboard at MatchEnded. The recap
        // reader must overlay those onto the event-derived aggregator output so the UI shows the
        // same numbers RL itself reported — and use Platform from PlayerMatchStats since
        // EventParticipants doesn't carry it.
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "MS-WITH-STATS",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
        });
        AddGoal(seedCtx, "MS-WITH-STATS", 1_100_000, scorerName: "Tobi", scorerTeamNum: 0, goalSpeed: 1500, goalTime: 30);
        seedCtx.PlayerMatchStats.Add(new PlayerMatchStats
        {
            MatchGuid = "MS-WITH-STATS",
            Shortcut = 1,
            PlayerName = "Tobi",
            TeamNum = 0,
            Platform = "Steam",
            Score = 540,
            Goals = 3,
            Assists = 2,
            Saves = 4,
            Shots = 7,
            Touches = 42,
        });
        await seedCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var recap = await reader.GetRecapAsync("MS-WITH-STATS", CancellationToken.None);

        Assert.NotNull(recap);
        var tobi = Assert.Single(recap!.PlayerStats);
        Assert.Equal(540, tobi.Score);
        Assert.Equal(3, tobi.Goals);
        Assert.Equal(2, tobi.Assists);
        Assert.Equal(4, tobi.Saves);
        Assert.Equal(7, tobi.Shots);
        Assert.Equal(42, tobi.Touches);
        Assert.Equal("Steam", tobi.Player.Platform);
    }

    [Fact]
    public async Task GetRecapAsync_keeps_event_derived_stats_when_no_persisted_player_stats()
    {
        // Pre-migration matches have no PlayerMatchStats rows but DO have EventParticipants
        // (which the writer has populated since v1). The reader must keep the aggregator's
        // event-derived values (and empty Platform) for those — never silently zero them out.
        await using var seedCtx = this.CreateDbContext();
        seedCtx.Matches.Add(new Match
        {
            MatchGuid = "MS-LEGACY",
            FirstSeenAtUtc = 1_000_000,
            EndedAtUtc = 1_300_000,
            LastEventAtUtc = 1_300_000,
        });
        AddGoal(seedCtx, "MS-LEGACY", 1_100_000, scorerName: "Tobi", scorerTeamNum: 0, goalSpeed: 1500, goalTime: 30);
        await seedCtx.SaveChangesAsync();

        // Seed the EventParticipant for the goal we just inserted — production writes both, but
        // AddGoal only writes the event row to keep the helper minimal.
        await using var partCtx = this.CreateDbContext();
        var goalEvent = await partCtx.Events.FirstAsync(e => e.MatchGuid == "MS-LEGACY");
        partCtx.EventParticipants.Add(new EventParticipant
        {
            EventId = goalEvent.Id,
            MatchGuid = "MS-LEGACY",
            PlayerName = "Tobi",
            Shortcut = 1,
            TeamNum = 0,
            Role = ParticipantRoles.Scorer,
            TimestampUtc = 1_100_000,
        });
        await partCtx.SaveChangesAsync();

        await using var ctx = this.CreateDbContext();
        var reader = new MatchHistoryReader(ctx);

        var recap = await reader.GetRecapAsync("MS-LEGACY", CancellationToken.None);

        Assert.NotNull(recap);
        var tobi = Assert.Single(recap!.PlayerStats);
        Assert.Equal(1, tobi.Goals);  // event-derived from the AddGoal call above
        Assert.Equal(0, tobi.Score);  // no persisted Score; aggregator fallback is 0
        Assert.Equal(string.Empty, tobi.Player.Platform);
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
