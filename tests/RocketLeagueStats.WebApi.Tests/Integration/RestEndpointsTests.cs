namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RocketLeagueStats.WebApi.Contracts;
using Xunit;

public sealed class RestEndpointsTests(WebHostFixture fixture) : IClassFixture<WebHostFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task GET_state_returns_idle_phase_initially()
    {
        var client = fixture.CreateClient();
        var dto = await client.GetFromJsonAsync<LiveStateDto>("/api/state", JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(MatchPhase.Idle, dto!.Phase);
    }

    [Fact]
    public async Task GET_matches_returns_empty_array_initially()
    {
        var client = fixture.CreateClient();
        var dtos = await client.GetFromJsonAsync<MatchSummaryDto[]>("/api/matches", JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos!);
    }

    [Fact]
    public async Task GET_match_recap_for_unknown_id_returns_404()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/matches/no-such", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_settings_then_GET_round_trips()
    {
        var client = fixture.CreateClient();
        var input = new SettingsDto("Hellcat", ["Stinkmaster"], ShowTrainingInHistory: true);
        var put = await client.PutAsJsonAsync("/api/settings", input, JsonOptions);
        put.EnsureSuccessStatusCode();

        var loaded = await client.GetFromJsonAsync<SettingsDto>("/api/settings", JsonOptions);
        Assert.Equal("Hellcat", loaded!.PlayerName);
        Assert.True(loaded.ShowTrainingInHistory);
    }

    [Fact]
    public async Task GET_health_returns_healthy()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }
}
