namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;
using Xunit;

// Standalone test class so we get a fresh WebHostFixture (and a fresh SQLite DB) — same reason
// as MatchRecapPersistenceTests: writing rows would otherwise pollute the "history is empty"
// assertions in RestEndpointsTests via xUnit's IClassFixture sharing.
public sealed class MatchDeletionTests(WebHostFixture fixture) : IClassFixture<WebHostFixture>
{
    [Fact]
    public async Task DELETE_match_cascades_to_all_related_tables()
    {
        // End-to-end: publish a complete match cycle (init + goal + snapshot + ended), wait for
        // the writer to flush, then DELETE /api/matches/{id} and verify Matches, Events,
        // MatchSnapshots, EventParticipants, and PlayerMatchStats all drop the related rows.
        // SQLite's FK cascade does the work — this test guards against accidental schema drift
        // (e.g. someone removing the OnDelete(Cascade) configuration).
        const string matchGuid = "DELETE-CASCADE-TEST-0001";
        var client = fixture.CreateClient();
        var bus = fixture.GetBus();

        bus.Publish(new MatchInitializedEvent { MatchGuid = matchGuid });
        bus.Publish(new GoalScoredEvent
        {
            MatchGuid = matchGuid,
            Scorer = new PlayerRef("Tobi", 1, 0),
            ImpactLocation = default,
        });
        using var snapshotJson = System.Text.Json.JsonDocument.Parse("""
            {
              "MatchGuid": "DELETE-CASCADE-TEST-0001",
              "Players": [
                { "Name": "Tobi", "PrimaryId": "Steam|1|0", "Shortcut": 1, "TeamNum": 0,
                  "Score": 100, "Goals": 1, "Assists": 0, "Saves": 0, "Shots": 1, "Touches": 5 }
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
        bus.Publish(new MatchStateSnapshot
        {
            MatchGuid = matchGuid,
            RawData = snapshotJson.RootElement.Clone(),
        });
        bus.Publish(new MatchEndedEvent { MatchGuid = matchGuid, WinnerTeamNum = 0 });

        // Wait for the writer to flush — terminal force-flush should land the rows within ~100ms.
        await this.WaitForRowsAsync(matchGuid, expected: true, deadlineSeconds: 5);

        // Sanity: every child table has at least one row tied to this match before we delete.
        await this.AssertChildRowCounts(matchGuid, expectedExists: true);

        var response = await client.DeleteAsync(new Uri($"/api/matches/{matchGuid}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // After delete, every table must be empty for this MatchGuid.
        await this.AssertChildRowCounts(matchGuid, expectedExists: false);
    }

    [Fact]
    public async Task DELETE_match_for_unknown_id_returns_404()
    {
        var client = fixture.CreateClient();
        var response = await client.DeleteAsync(new Uri("/api/matches/no-such-match", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task WaitForRowsAsync(string matchGuid, bool expected, int deadlineSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(deadlineSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StatsDbContext>();
            var present = await db.Matches.AnyAsync(m => m.MatchGuid == matchGuid);
            if (present == expected)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Timed out waiting for match {matchGuid} present={expected}.");
    }

    private async Task AssertChildRowCounts(string matchGuid, bool expectedExists)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StatsDbContext>();
        var matchCount = await db.Matches.CountAsync(m => m.MatchGuid == matchGuid);
        var eventCount = await db.Events.CountAsync(e => e.MatchGuid == matchGuid);
        var snapshotCount = await db.MatchSnapshots.CountAsync(s => s.MatchGuid == matchGuid);
        var participantCount = await db.EventParticipants.CountAsync(p => p.MatchGuid == matchGuid);
        var playerStatsCount = await db.PlayerMatchStats.CountAsync(p => p.MatchGuid == matchGuid);

        if (expectedExists)
        {
            Assert.Equal(1, matchCount);
            Assert.True(eventCount > 0, "Expected at least one Events row before delete.");
            Assert.True(snapshotCount > 0, "Expected at least one MatchSnapshots row before delete.");
            Assert.True(participantCount > 0, "Expected at least one EventParticipants row before delete.");
            Assert.True(playerStatsCount > 0, "Expected at least one PlayerMatchStats row before delete.");
        }
        else
        {
            Assert.Equal(0, matchCount);
            Assert.Equal(0, eventCount);
            Assert.Equal(0, snapshotCount);
            Assert.Equal(0, participantCount);
            Assert.Equal(0, playerStatsCount);
        }
    }
}
