namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class GetMatchHistoryHandler(MatchHistoryReader reader)
    : IQueryHandler<GetMatchHistoryQuery, MatchSummaryDto[]>
{
    public async ValueTask<MatchSummaryDto[]> Handle(GetMatchHistoryQuery query, CancellationToken ct)
    {
        var filter = new HistoryFilter(query.IncludeTraining, query.IncludeFreePlay, query.From, query.To, query.Sort);
        return await reader.GetMatchesAsync(filter, ct);
    }
}
