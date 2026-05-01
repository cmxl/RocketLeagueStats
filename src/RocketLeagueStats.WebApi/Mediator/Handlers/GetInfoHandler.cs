namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;

internal sealed class GetInfoHandler : IQueryHandler<GetInfoQuery, ServerInfoDto>
{
    public ValueTask<ServerInfoDto> Handle(GetInfoQuery query, CancellationToken ct)
    {
        var asm = typeof(GetInfoHandler).Assembly;
        var version = asm.GetName().Version?.ToString() ?? "0.0.0";
        // Use AppContext.BaseDirectory for single-file publish compatibility (IL3000)
        var buildDate = File.GetLastWriteTimeUtc(AppContext.BaseDirectory);
        return ValueTask.FromResult(new ServerInfoDto(
            Version: version,
            BuildDate: buildDate,
            EnabledFeatures: ["live", "history", "recap", "settings"]));
    }
}
