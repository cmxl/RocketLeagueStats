namespace RocketLeagueStats.WebApi.Tests.Integration;

using Xunit;

public sealed class FixtureSmokeTests(WebHostFixture fixture) : IClassFixture<WebHostFixture>
{
    [Fact]
    public async Task Info_endpoint_responds()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/info", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }
}
