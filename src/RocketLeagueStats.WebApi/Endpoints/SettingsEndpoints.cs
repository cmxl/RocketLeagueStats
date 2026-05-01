namespace RocketLeagueStats.WebApi.Endpoints;

using global::Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mediator.Queries;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetSettingsQuery(), ct)))
            .WithName("GetSettings");

        app.MapPut("/api/settings", async (SettingsDto settings, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new UpdateSettingsCommand(settings), ct)))
            .WithName("UpdateSettings");

        return app;
    }
}
