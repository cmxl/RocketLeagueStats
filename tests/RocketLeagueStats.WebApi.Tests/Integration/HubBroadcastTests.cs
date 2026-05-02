namespace RocketLeagueStats.WebApi.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;
using Xunit;

public sealed class HubBroadcastTests(WebHostFixture fixture) : IClassFixture<WebHostFixture>
{
    private const string OnlineMatchGuid = "TEST-MATCH-GUID-0001";

    [Fact]
    public async Task MatchInitialized_for_online_match_broadcasts_OnPhaseChanged_to_clients()
    {
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        fixture.GetBus().Publish(new MatchInitializedEvent { MatchGuid = OnlineMatchGuid });
        await Task.Delay(500);

        Assert.Contains(MatchPhase.Live, phaseChanges);
    }

    [Fact]
    public async Task MatchInitialized_with_empty_MatchGuid_is_not_tracked_as_live()
    {
        // Training / free-play / private-match events arrive with an empty MatchGuid. By
        // project policy these are NOT tracked as live (no live UI, no history, no recap),
        // so the projector returns early and never emits OnPhaseChanged(Live).
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        var bus = fixture.GetBus();
        bus.Publish(new MatchInitializedEvent { MatchGuid = string.Empty });
        bus.Publish(new MatchDestroyedEvent { MatchGuid = string.Empty });
        await Task.Delay(500);

        Assert.DoesNotContain(MatchPhase.Live, phaseChanges);
    }

    [Fact]
    public async Task MatchDestroyed_alone_concludes_an_online_match_and_returns_to_idle()
    {
        // Some online matches end with MatchDestroyed only — no preceding MatchEnded
        // (e.g., player disconnect cases). The projector must still drive Idle on a
        // bare MatchDestroyed for a tracked (non-empty MatchGuid) match.
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        var bus = fixture.GetBus();
        bus.Publish(new MatchInitializedEvent { MatchGuid = OnlineMatchGuid });
        await Task.Delay(200);
        bus.Publish(new MatchDestroyedEvent { MatchGuid = OnlineMatchGuid });
        await Task.Delay(500);

        Assert.Contains(MatchPhase.Live, phaseChanges);
        Assert.Contains(MatchPhase.Idle, phaseChanges);
    }

    [Fact]
    public async Task MatchEnded_followed_by_MatchDestroyed_idempotent_only_one_idle_phase()
    {
        // Ranked matches fire MatchEnded then MatchDestroyed. The second event should
        // be a no-op — state is already idle. Both broadcast on the same wire, but the
        // second broadcast cycle finds nothing to broadcast.
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        var bus = fixture.GetBus();
        bus.Publish(new MatchInitializedEvent { MatchGuid = OnlineMatchGuid });
        await Task.Delay(200);
        bus.Publish(new MatchEndedEvent { MatchGuid = OnlineMatchGuid });
        bus.Publish(new MatchDestroyedEvent { MatchGuid = OnlineMatchGuid });
        await Task.Delay(500);

        // Live → Idle once. The second MatchDestroyed shouldn't double-broadcast.
        Assert.Equal([MatchPhase.Live, MatchPhase.Idle], phaseChanges);
    }

    private Task<HubConnection> ConnectHubAsync()
    {
        var client = fixture.CreateClient();
        var url = client.BaseAddress + "hub/stats";
        var hub = new HubConnectionBuilder()
            .WithUrl(url, opts => opts.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler())
            .AddJsonProtocol(opts =>
            {
                opts.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            })
            .Build();
        return Task.FromResult(hub);
    }
}
