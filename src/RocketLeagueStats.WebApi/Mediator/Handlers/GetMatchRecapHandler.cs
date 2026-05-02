namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class GetMatchRecapHandler(MatchHistoryReader reader)
    : IQueryHandler<GetMatchRecapQuery, MatchRecapDto?>
{
    public async ValueTask<MatchRecapDto?> Handle(GetMatchRecapQuery query, CancellationToken ct) =>
        await reader.GetRecapAsync(query.MatchId, ct);
}
