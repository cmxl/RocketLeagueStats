namespace RocketLeagueStats.Core.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Connection;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.GameSetup;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRocketLeagueStatsCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StatsApiOptions>(configuration.GetSection(StatsApiOptions.SectionName));
        services.Configure<EventLogOptions>(configuration.GetSection(EventLogOptions.SectionName));
        services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));
        services.Configure<GameSetupOptions>(configuration.GetSection(GameSetupOptions.SectionName));
        services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));

        services.AddSingleton<StatsEventParser>();
        services.AddSingleton<StatsEventBus>();

        services.AddSingleton<IStatsApiClient, StatsApiClient>();

        services.AddSingleton<IProcessLookup, ProcessLookup>();
        services.AddSingleton<GameInstallLocator.Probes>(_ => GameInstallLocator.Probes.Default());
        services.AddSingleton<IGameInstallLocator, GameInstallLocator>();
        services.AddSingleton<IStatsApiConfigWriter, StatsApiConfigWriter>();

        // Resolve the connection string LAZILY (factory delegates) rather than at registration
        // time. WebApplicationFactory tests inject ConnectionStrings:Stats overrides via
        // ConfigureAppConfiguration AFTER this method runs; resolving eagerly here would capture
        // the user's real %LocalAppData% DB before the test config can override it.
        services.AddSingleton(sp => new EventStoreConnectionString(
            StatsConnectionString.Resolve(sp.GetRequiredService<IConfiguration>())));
        services.AddDbContext<StatsDbContext>((sp, opts) =>
        {
            var conn = StatsConnectionString.Resolve(sp.GetRequiredService<IConfiguration>());
            opts.UseSqlite(conn);
        });

        return services;
    }

    public static IServiceCollection AddRocketLeagueStatsHostingDefaults(this IServiceCollection services)
    {
        // EventStoreStartupService runs migrations + logs path/size before any other hosted service
        // touches the DB; subsequent services depend on the schema being present.
        services.AddHostedService<EventStoreStartupService>();
        services.AddHostedService<IniBootstrapHostedService>();
        services.AddHostedService<StatsApiListenerService>();
        services.AddHostedService<JsonlEventLoggerService>();
        services.AddHostedService<SnapshotDumperService>();
        services.AddHostedService<SqliteEventStoreService>();
        return services;
    }
}
