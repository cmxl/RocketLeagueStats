namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class GetSettingsHandler(ISettingsStore store)
    : IQueryHandler<GetSettingsQuery, SettingsDto>
{
    public async ValueTask<SettingsDto> Handle(GetSettingsQuery query, CancellationToken ct) =>
        await store.GetAsync(ct);
}
