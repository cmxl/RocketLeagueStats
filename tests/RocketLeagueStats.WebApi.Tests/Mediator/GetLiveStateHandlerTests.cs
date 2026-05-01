namespace RocketLeagueStats.WebApi.Tests.Mediator;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Handlers;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class GetLiveStateHandlerTests
{
    [Fact]
    public async Task Returns_state_from_LiveMatchState()
    {
        var state = new LiveMatchState();
        var handler = new GetLiveStateHandler(state);
        var result = await handler.Handle(new GetLiveStateQuery(), CancellationToken.None);
        Assert.Equal(MatchPhase.Idle, result.Phase);
    }
}
