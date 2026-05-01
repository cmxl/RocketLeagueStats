namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class GetMatchRecapHandler(IMatchHistoryIndex index)
    : IQueryHandler<GetMatchRecapQuery, MatchRecapDto?>
{
    public ValueTask<MatchRecapDto?> Handle(GetMatchRecapQuery query, CancellationToken ct) =>
        ValueTask.FromResult(index.GetRecap(query.MatchId));
}
