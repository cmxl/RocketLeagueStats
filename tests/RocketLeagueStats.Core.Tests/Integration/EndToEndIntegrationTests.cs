using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Connection;
using RocketLeagueStats.Core.DependencyInjection;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.GameSetup;
using RocketLeagueStats.Core.Tests.Connection;

namespace RocketLeagueStats.Core.Tests.Integration;

public class EndToEndIntegrationTests
{
    [Fact]
    public async Task Listener_publishes_events_to_a_subscriber_through_the_real_DI_pipeline()
    {
        var lines = new[]
        {
            """{"Event":"MatchInitialized","MatchGuid":"x","Data":{"MatchGuid":"x"}}""",
            """{"Event":"GoalScored","MatchGuid":"x","Data":{"GoalSpeed":1834.5,"GoalTime":127.5,"ImpactLocation":{"X":0,"Y":-2944,"Z":320},"Scorer":{"Name":"Karbon","Shortcut":1,"TeamNum":0}}}""",
            "{not_json_but_balanced}",
            """{"Event":"MatchEnded","MatchGuid":"x","Data":{"WinnerTeamNum":0}}""",
        };

        await using var server = FakeStatsApiServer.Start(lines);

        var configurationData = new Dictionary<string, string?>
        {
            ["StatsApi:Port"] = server.Port.ToString(CultureInfo.InvariantCulture),
            ["StatsApi:ConnectRetry:InitialDelay"] = "00:00:00.100",
            ["StatsApi:ConnectRetry:MaxDelay"] = "00:00:01",
            ["StatsApi:ConnectRetry:MaxAttempts"] = "10000",   // don't let retries exhaust during the test window
            ["EventLog:Enabled"] = "false",
            ["GameSetup:AutoConfigureIni"] = "false",
        };

        var collected = Channel.CreateUnbounded<StatsEvent>();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configurationData);

        // Skip ini bootstrap entirely by substituting the locator to return null.
        var locator = Substitute.For<IGameInstallLocator>();
        locator.Locate().Returns((RocketLeagueInstall?)null);

        builder.Services.AddRocketLeagueStatsCore(builder.Configuration);
        builder.Services.AddSingleton(locator);                                 // override real locator
        builder.Services.AddHostedService(sp => new ProbeHostedService(
            sp.GetRequiredService<StatsEventBus>(), collected.Writer));
        builder.Services.AddHostedService(sp =>
            new StatsApiListenerService(
                sp.GetRequiredService<IStatsApiClient>(),
                sp.GetRequiredService<IOptions<StatsApiOptions>>(),
                sp.GetRequiredService<ILogger<StatsApiListenerService>>()));

        using var host = builder.Build();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);

        var received = new List<StatsEvent>();
        try
        {
            // Read 3 events (the malformed line is skipped)
            for (var i = 0; i < 3; i++)
            {
                received.Add(await collected.Reader.ReadAsync(cts.Token));
            }
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }

        Assert.Collection(
            received,
            e => Assert.IsType<MatchInitializedEvent>(e),
            e => Assert.IsType<GoalScoredEvent>(e),
            e => Assert.IsType<MatchEndedEvent>(e));
    }

    private sealed class ProbeHostedService(StatsEventBus bus, ChannelWriter<StatsEvent> writer) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = bus.Subscribe();
            try
            {
                await foreach (var evt in reader.ReadAllAsync(stoppingToken))
                {
                    await writer.WriteAsync(evt, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                /* expected during host shutdown */
            }
        }
    }
}
