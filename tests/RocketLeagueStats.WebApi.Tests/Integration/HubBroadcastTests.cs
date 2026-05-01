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
    [Fact]
    public async Task MatchInitialized_event_broadcasts_OnPhaseChanged_to_clients()
    {
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        fixture.GetBus().Publish(new MatchInitializedEvent());
        await Task.Delay(500);

        Assert.Contains(MatchPhase.Live, phaseChanges);
    }

    [Fact]
    public async Task MatchDestroyed_alone_concludes_a_training_match_and_returns_to_idle()
    {
        // Training and free-play sessions end with MatchDestroyed only — no MatchEnded.
        // Verified in real captures (rl-stats-2026-05-01.jsonl lines 172, 1462, 2194:
        // empty MatchGuid, MatchDestroyed with no preceding MatchEnded).
        await using var hub = await this.ConnectHubAsync();
        var phaseChanges = new List<MatchPhase>();
        hub.On<MatchPhase>("OnPhaseChanged", phaseChanges.Add);
        await hub.StartAsync();
        await Task.Delay(200);

        var bus = fixture.GetBus();
        bus.Publish(new MatchInitializedEvent());
        await Task.Delay(200);
        bus.Publish(new MatchDestroyedEvent());
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
        bus.Publish(new MatchInitializedEvent());
        await Task.Delay(200);
        bus.Publish(new MatchEndedEvent());
        bus.Publish(new MatchDestroyedEvent());
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
