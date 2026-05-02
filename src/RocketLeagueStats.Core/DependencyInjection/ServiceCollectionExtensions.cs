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

        var connectionString = StatsConnectionString.Resolve(configuration);
        services.AddSingleton(new EventStoreConnectionString(connectionString));
        services.AddDbContext<StatsDbContext>(opts => opts.UseSqlite(connectionString));

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
