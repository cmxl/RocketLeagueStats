namespace RocketLeagueStats.WebApi.Endpoints;

using global::Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RocketLeagueStats.WebApi.Mediator.Queries;

internal static class StateEndpoints
{
    public static IEndpointRouteBuilder MapStateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/state", async (IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetLiveStateQuery(), ct)))
            .WithName("GetLiveState");

        return app;
    }
}
