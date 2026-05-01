namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class GetLiveStateHandler(LiveMatchState state) : IQueryHandler<GetLiveStateQuery, LiveStateDto>
{
    public ValueTask<LiveStateDto> Handle(GetLiveStateQuery query, CancellationToken ct) =>
        ValueTask.FromResult(state.ToLiveStateDto());
}
