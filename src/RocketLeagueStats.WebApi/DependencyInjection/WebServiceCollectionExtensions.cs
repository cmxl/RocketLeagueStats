namespace RocketLeagueStats.WebApi.DependencyInjection;

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RocketLeagueStats.WebApi.Services;

public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddRocketLeagueStatsWebApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Mediator (source-generated AddMediator extension)
        services.AddMediator();

        // REST JSON: camelCase property names + camelCase enum names
        // (the spec mandates camelCase wire format; default JsonStringEnumConverter
        // emits PascalCase enum names which would mismatch TypeScript clients).
        services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        // SignalR: same camelCase convention end-to-end
        services.AddSignalR()
            .AddJsonProtocol(opts =>
            {
                opts.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        // Response compression: Brotli first (preferred when the client lists
        // it in Accept-Encoding), Gzip fallback. SmallestSize matches the
        // Microsoft Learn recommendation for "few large response bodies
        // repeated often" - exactly our case (the largest Angular chunk is
        // ~560 KB, served unchanged for the lifetime of its content hash).
        // The CPU cost per compression amortizes against the year-long
        // immutable client cache: each chunk hash is encoded once per cold
        // visitor and never re-encoded for that user again.
        //
        // application/manifest+json is added on top of the framework defaults
        // (which already cover application/javascript, text/css, text/html,
        // application/json, etc.).
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes
                .Concat(["application/manifest+json"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.SmallestSize);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.SmallestSize);

        // OpenAPI (Microsoft.AspNetCore.OpenApi)
        services.AddOpenApi();

        // Health checks
        services.AddHealthChecks();

        // Domain singletons
        services.AddSingleton<LiveMatchState>();
        services.AddScoped<MatchHistoryReader>();

        // SettingsStore — directory configurable via Web:SettingsDirectory; defaults to
        // %APPDATA%/RocketLeagueStats. Resolved lazily inside the factory delegate (not at
        // registration time) so WebApplicationFactory test overrides land before the directory
        // is opened. Treat empty-string as missing so the appsettings.json placeholder
        // ("SettingsDirectory": "") still hits the default branch.
        services.AddSingleton<ISettingsStore>(_ =>
        {
            var configured = configuration["Web:SettingsDirectory"];
            var dir = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RocketLeagueStats")
                : configured;
            return new SettingsStore(dir);
        });

        // Hosted services that subscribe to the bus
        services.AddHostedService<LiveMatchProjector>();

        // CORS for ng-serve dev mode (localhost:4200)
        services.AddCors(opts =>
            opts.AddPolicy("ng-serve-dev", policy =>
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()));

        return services;
    }
}
