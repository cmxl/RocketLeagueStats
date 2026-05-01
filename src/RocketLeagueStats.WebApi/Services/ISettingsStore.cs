namespace RocketLeagueStats.WebApi.Services;

using RocketLeagueStats.WebApi.Contracts;

public interface ISettingsStore
{
    public Task<SettingsDto> GetAsync(CancellationToken ct);

    public Task SaveAsync(SettingsDto settings, CancellationToken ct);
}
