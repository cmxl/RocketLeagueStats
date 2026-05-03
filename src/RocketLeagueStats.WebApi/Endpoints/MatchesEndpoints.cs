namespace RocketLeagueStats.WebApi.Endpoints;

using global::Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RocketLeagueStats.WebApi.Mediator.Queries;
using RocketLeagueStats.WebApi.Services;

internal static class MatchesEndpoints
{
    public static IEndpointRouteBuilder MapMatchesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/matches", async (
                IMediator mediator,
                bool? includeTraining,
                bool? includeFreePlay,
                DateTime? from,
                DateTime? to,
                string? sort,
                CancellationToken ct) =>
            {
                var sortMode = (sort ?? string.Empty).ToLowerInvariant() switch
                {
                    "highscoring" or "highest-scoring" or "highestscoring" => HistorySort.HighestScoring,
                    _ => HistorySort.MostRecent,
                };
                var query = new GetMatchHistoryQuery(
                    IncludeTraining: includeTraining ?? false,
                    IncludeFreePlay: includeFreePlay ?? false,
                    From: from,
                    To: to,
                    Sort: sortMode);
                return Results.Ok(await mediator.Send(query, ct));
            })
            .WithName("GetMatchHistory");

        app.MapGet("/api/matches/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
            {
                var recap = await mediator.Send(new GetMatchRecapQuery(id), ct);
                return recap is null ? Results.NotFound() : Results.Ok(recap);
            })
            .WithName("GetMatchRecap");

        app.MapDelete("/api/matches/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
            {
                var deleted = await mediator.Send(new DeleteMatchCommand(id), ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteMatch");

        return app;
    }
}
