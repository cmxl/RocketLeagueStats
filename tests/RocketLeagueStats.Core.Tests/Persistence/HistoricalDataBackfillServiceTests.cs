namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.Core.Persistence.Entities;

/// <summary>
/// Verifies the backfill service populates team metadata + PlayerMatchStats for matches that
/// were persisted before the projection logic shipped (commit 8080c69). Every test starts from
/// a Matches row with a stored MatchSnapshot but NULL team/arena and no PlayerMatchStats rows —
/// the same shape the user's stats.db ended up in after running an older binary against the
/// post-migration schema.
/// </summary>
public sealed class HistoricalDataBackfillServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => this.fixture.InitializeAsync();

    public Task DisposeAsync() => this.fixture.DisposeAsync();

    public void Dispose() => this.fixture.Dispose();

    private const string SnapshotJson =
        """
        {
          "MatchGuid": "match-1",
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
        """;

    [Fact]
    public async Task Backfill_FillsArenaTeamMetadataAndPlayerStats_FromLatestSnapshot()
    {
        await this.SeedEndedMatchAsync("match-1", SnapshotJson, includePlayerStats: false, populateArena: false);

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-1");
        Assert.Equal("DFH Stadium", match.Arena);
        Assert.Equal("BLUE", match.BlueTeamName);
        Assert.Equal("1873FF", match.BlueColorPrimary);
        Assert.Equal("0F3D8A", match.BlueColorSecondary);
        Assert.Equal("ORANGE", match.OrangeTeamName);
        Assert.Equal("F08020", match.OrangeColorPrimary);

        var stats = await ctx.PlayerMatchStats
            .Where(p => p.MatchGuid == "match-1")
            .OrderBy(p => p.Shortcut)
            .ToListAsync();
        Assert.Equal(2, stats.Count);
        Assert.Equal("Tobi", stats[0].PlayerName);
        Assert.Equal("Steam", stats[0].Platform);
        Assert.Equal(540, stats[0].Score);
        Assert.Equal(3, stats[0].Goals);
        Assert.Equal(42, stats[0].Touches);
        Assert.Equal("Epic", stats[1].Platform);
        Assert.Equal(320, stats[1].Score);
    }

    [Fact]
    public async Task Backfill_IsIdempotent_DoesNotDuplicatePlayerStatsOnRerun()
    {
        await this.SeedEndedMatchAsync("match-1", SnapshotJson, includePlayerStats: false, populateArena: false);

        await this.RunBackfillAsync();
        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(2, await ctx.PlayerMatchStats.CountAsync(p => p.MatchGuid == "match-1"));
    }

    [Fact]
    public async Task Backfill_DoesNotOverwriteExistingArenaOrTeamMetadata()
    {
        await this.SeedEndedMatchAsync(
            "match-1",
            SnapshotJson,
            includePlayerStats: false,
            populateArena: true,
            arenaOverride: "PreExisting Arena",
            blueNameOverride: "Existing Blue");

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-1");
        // Already-populated columns are kept verbatim; only NULL columns get filled.
        Assert.Equal("PreExisting Arena", match.Arena);
        Assert.Equal("Existing Blue", match.BlueTeamName);
        // Sibling NULLs get filled from the snapshot, even when one of the columns was set already.
        Assert.Equal("1873FF", match.BlueColorPrimary);
        Assert.Equal("ORANGE", match.OrangeTeamName);
    }

    [Fact]
    public async Task Backfill_SkipsMatchesWithoutAnySnapshot()
    {
        await this.SeedEndedMatchAsync("match-no-snap", snapshotJson: null, includePlayerStats: false, populateArena: false);

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-no-snap");
        Assert.Null(match.Arena);
        Assert.Empty(await ctx.PlayerMatchStats.Where(p => p.MatchGuid == "match-no-snap").ToListAsync());
    }

    [Fact]
    public async Task Backfill_SkipsInProgressMatches()
    {
        // EndedAtUtc null -> still in progress; the live writer will fill team metadata when it ends.
        // Backfill only touches concluded rows so we don't race the live path on the active match.
        await this.SeedEndedMatchAsync(
            "match-live",
            SnapshotJson,
            includePlayerStats: false,
            populateArena: false,
            ended: false);

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        var match = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-live");
        Assert.Null(match.Arena);
        Assert.Empty(await ctx.PlayerMatchStats.Where(p => p.MatchGuid == "match-live").ToListAsync());
    }

    [Fact]
    public async Task Backfill_AddsPlayerStatsEvenWhenArenaAlreadyFilled()
    {
        // Symmetric edge case: arena/colors persisted by the live writer but the player-stats
        // pass somehow got skipped (e.g. partial-rollout build with the team-metadata fix but
        // not the per-player one). Backfill should still finish the job.
        await this.SeedEndedMatchAsync(
            "match-1",
            SnapshotJson,
            includePlayerStats: false,
            populateArena: true);

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        Assert.Equal(2, await ctx.PlayerMatchStats.CountAsync(p => p.MatchGuid == "match-1"));
    }

    [Fact]
    public async Task Backfill_HandlesMalformedSnapshotPayloadGracefully()
    {
        // Defensive: a corrupted Payload row should not crash the service or block backfill of
        // other matches. We seed the malformed match plus a healthy one and assert the healthy
        // one still gets filled while the broken one is skipped.
        await this.SeedEndedMatchAsync("match-broken", "{not valid json", includePlayerStats: false, populateArena: false);
        await this.SeedEndedMatchAsync("match-1", SnapshotJson, includePlayerStats: false, populateArena: false);

        await this.RunBackfillAsync();

        await using var ctx = this.fixture.CreateDbContext();
        var broken = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-broken");
        Assert.Null(broken.Arena);
        var good = await ctx.Matches.SingleAsync(m => m.MatchGuid == "match-1");
        Assert.Equal("DFH Stadium", good.Arena);
    }

    private async Task RunBackfillAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StatsDbContext>(opts => opts.UseSqlite(this.fixture.ConnectionString));
        services.AddSingleton<HistoricalDataBackfillService>();
        await using var sp = services.BuildServiceProvider();

        var service = new HistoricalDataBackfillService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<HistoricalDataBackfillService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);
    }

    private async Task SeedEndedMatchAsync(
        string matchGuid,
        string? snapshotJson,
        bool includePlayerStats,
        bool populateArena,
        bool ended = true,
        string? arenaOverride = null,
        string? blueNameOverride = null)
    {
        await using var ctx = this.fixture.CreateDbContext();
        var match = new Match
        {
            MatchGuid = matchGuid,
            FirstSeenAtUtc = 1_000,
            EndedAtUtc = ended ? 60_000L : null,
            EventCount = 0,
            SnapshotCount = 0,
            LastEventAtUtc = 60_000,
            WinnerTeamNum = ended ? 0 : null,
            Arena = populateArena ? (arenaOverride ?? "PreExisting Arena") : null,
            BlueTeamName = populateArena ? (blueNameOverride ?? "PreExisting Blue") : null,
        };
        ctx.Matches.Add(match);

        if (snapshotJson is not null)
        {
            ctx.MatchSnapshots.Add(new MatchSnapshotRecord
            {
                MatchGuid = matchGuid,
                TimestampUtc = 30_000,
                Payload = snapshotJson,
            });
        }

        if (includePlayerStats)
        {
            ctx.PlayerMatchStats.Add(new PlayerMatchStats
            {
                MatchGuid = matchGuid,
                Shortcut = 1,
                PlayerName = "Tobi",
                TeamNum = 0,
                Platform = "Steam",
                Score = 100,
                Goals = 0,
                Assists = 0,
                Saves = 0,
                Shots = 0,
                Touches = 0,
            });
            ctx.PlayerMatchStats.Add(new PlayerMatchStats
            {
                MatchGuid = matchGuid,
                Shortcut = 2,
                PlayerName = "Vex",
                TeamNum = 1,
                Platform = "Epic",
                Score = 80,
                Goals = 0,
                Assists = 0,
                Saves = 0,
                Shots = 0,
                Touches = 0,
            });
        }

        await ctx.SaveChangesAsync();
    }
}
