namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;
using Xunit;

// Standalone test class so we get a fresh WebHostFixture (and a fresh SQLite DB) — the recap
// scenario writes rows that would otherwise leak into RestEndpointsTests' "matches list is
// empty initially" assertion since xUnit's IClassFixture is shared across all tests in a class.
public sealed class MatchRecapPersistenceTests(WebHostFixture fixture) : IClassFixture<WebHostFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task GET_match_recap_after_match_cycle_returns_persisted_recap()
    {
        // Regression for the "show recap" popup invalid-reference bug. Until this fix the live
        // projector minted a synthetic Guid as the live MatchId, but the DB-backed history reader
        // queries by wire MatchGuid — so any recap link from the live UI 404'd. The projector now
        // uses evt.MatchGuid directly, and the writer force-flushes on terminal events so the row
        // is persisted before the user's click reaches the GET endpoint.
        const string matchGuid = "RECAP-TEST-MATCH-0001";
        var client = fixture.CreateClient();
        var bus = fixture.GetBus();

        bus.Publish(new MatchInitializedEvent { MatchGuid = matchGuid });
        bus.Publish(new MatchEndedEvent { MatchGuid = matchGuid, WinnerTeamNum = 0 });

        // Even with force-flush on terminal, give the writer a moment to commit the SQLite txn
        // (the bus → writer subscription is async). Poll up to 5s; should resolve in under 500ms.
        MatchRecapDto? recap = null;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync(new Uri($"/api/matches/{matchGuid}", UriKind.Relative));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                recap = await response.Content.ReadFromJsonAsync<MatchRecapDto>(JsonOptions);
                break;
            }

            await Task.Delay(50);
        }

        Assert.NotNull(recap);
        Assert.Equal(matchGuid, recap!.Summary.MatchId);
    }
}
