namespace RocketLeagueStats.WebApi.Tests.Mediator;

using RocketLeagueStats.WebApi.Mediator.Handlers;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class GetMatchHistoryHandlerTests
{
    [Fact]
    public async Task Returns_filtered_match_summaries()
    {
        var index = new MatchHistoryIndex();
        var handler = new GetMatchHistoryHandler(index);
        var result = await handler.Handle(
            new GetMatchHistoryQuery(IncludeTraining: false, IncludeFreePlay: false, From: null, To: null, Sort: HistorySort.MostRecent),
            CancellationToken.None);
        Assert.Empty(result);
    }
}
