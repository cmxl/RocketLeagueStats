namespace RocketLeagueStats.WebApi.DependencyInjection;

using System.Text.Json;
using System.Text.Json.Serialization;
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

        // OpenAPI (Microsoft.AspNetCore.OpenApi)
        services.AddOpenApi();

        // Health checks
        services.AddHealthChecks();

        // Domain singletons
        services.AddSingleton<LiveMatchState>();
        services.AddSingleton<IMatchHistoryIndex, MatchHistoryIndex>();

        // SettingsStore — directory configurable via Web:SettingsDirectory (defaults to %APPDATA%/RocketLeagueStats)
        var settingsDir = configuration["Web:SettingsDirectory"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RocketLeagueStats");
        services.AddSingleton<ISettingsStore>(_ => new SettingsStore(settingsDir));

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
