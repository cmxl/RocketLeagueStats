namespace RocketLeagueStats.WebApi.DependencyInjection;

using Microsoft.AspNetCore.Builder;
using RocketLeagueStats.WebApi.Endpoints;
using RocketLeagueStats.WebApi.Hubs;

public static class WebApplicationExtensions
{
    public static WebApplication UseRocketLeagueStatsWebApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("ng-serve-dev");
            app.MapOpenApi();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapHealthChecks("/health");
        app.MapHub<StatsHub>("/hub/stats");

        app.MapStateEndpoints();
        app.MapMatchesEndpoints();
        app.MapSettingsEndpoints();
        app.MapInfoEndpoints();

        // SPA fallback — any unmatched non-API request gets index.html
        app.MapFallbackToFile("index.html");

        return app;
    }
}
