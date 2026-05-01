namespace RocketLeagueStats.WebApi.Endpoints;

using global::Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RocketLeagueStats.WebApi.Mediator.Queries;

internal static class InfoEndpoints
{
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/info", async (IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetInfoQuery(), ct)))
            .WithName("GetInfo");
        return app;
    }
}
