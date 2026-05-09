namespace RocketLeagueStats.WebApi.DependencyInjection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using RocketLeagueStats.WebApi.Endpoints;
using RocketLeagueStats.WebApi.Hubs;

public static class WebApplicationExtensions
{
    // Cache durations chosen against the Lighthouse 'uses-long-cache-ttl'
    // audit thresholds: >= 1 year scores 1.0, >= 30 days passes the audit.
    private const int OneYearSeconds = 31_536_000;
    private const int ThirtyDaysSeconds = 2_592_000;

    // Stable URLs whose body changes between deploys - must revalidate so
    // a deploy propagates immediately. ETag-based revalidation by the
    // StaticFileMiddleware keeps the cost at one 304 roundtrip per load.
    private static readonly HashSet<string> NoCacheFiles =
        new(StringComparer.OrdinalIgnoreCase) { "sw.js", "index.html", "offline.html" };

    // Stable-URL files that rarely change. 30 days is the audit pass-threshold;
    // the trade-off is a stale favicon/manifest window of up to a month after
    // any rebrand.
    private static readonly HashSet<string> ModerateCacheFiles =
        new(StringComparer.OrdinalIgnoreCase) { "favicon.ico", "manifest.webmanifest" };

    public static WebApplication UseRocketLeagueStatsWebApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("ng-serve-dev");
            app.MapOpenApi();
        }

        // UseResponseCompression must run before any middleware that writes a
        // response body we want compressed. Registering after UseStaticFiles
        // would leave the body already on the wire by the time compression
        // sees the request.
        app.UseResponseCompression();

        // Single options instance, shared with MapFallbackToFile below so the
        // SPA shell served on unmatched routes (/live, /history, ...) gets the
        // same Cache-Control treatment - MapFallbackToFile builds its own
        // sub-pipeline and does NOT inherit options from app.UseStaticFiles.
        var staticFileOptions = new StaticFileOptions
        {
            OnPrepareResponse = ApplyCacheHeaders,
        };

        app.UseDefaultFiles();
        app.UseStaticFiles(staticFileOptions);

        app.MapHealthChecks("/health");
        app.MapHub<StatsHub>("/hub/stats");

        app.MapStateEndpoints();
        app.MapMatchesEndpoints();
        app.MapSettingsEndpoints();
        app.MapInfoEndpoints();

        // SPA fallback — any unmatched non-API request gets index.html.
        // Pass the same options so the shell served here also gets no-cache.
        app.MapFallbackToFile("index.html", staticFileOptions);

        return app;
    }

    private static void ApplyCacheHeaders(StaticFileResponseContext ctx)
    {
        var name = ctx.File.Name;
        var headers = ctx.Context.Response.Headers;

        if (NoCacheFiles.Contains(name))
        {
            headers.CacheControl = "no-cache";
        }
        else if (ModerateCacheFiles.Contains(name))
        {
            headers.CacheControl = $"public, max-age={ThirtyDaysSeconds}";
        }
        else
        {
            // Everything else in wwwroot is content-hashed by Angular
            // (angular.json -> outputHashing: "all"), so the bytes for a
            // given URL never change. RFC 8246 'immutable' tells browsers
            // to skip even revalidation on F5. Source changes roll the
            // hash, producing a new URL and a fresh cache entry.
            headers.CacheControl = $"public, max-age={OneYearSeconds}, immutable";
        }
    }
}
