namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal sealed class UpdateSettingsHandler(ISettingsStore store)
    : ICommandHandler<UpdateSettingsCommand, SettingsDto>
{
    public async ValueTask<SettingsDto> Handle(UpdateSettingsCommand cmd, CancellationToken ct)
    {
        await store.SaveAsync(cmd.Settings, ct);
        return cmd.Settings;
    }
}
