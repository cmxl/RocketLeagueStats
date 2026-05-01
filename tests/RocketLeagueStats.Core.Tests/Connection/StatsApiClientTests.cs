using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RocketLeagueStats.Core.Bus;
using RocketLeagueStats.Core.Configuration;
using RocketLeagueStats.Core.Connection;
using RocketLeagueStats.Core.Events;

namespace RocketLeagueStats.Core.Tests.Connection;

public class StatsApiClientTests
{
    private static StatsApiClient BuildClient(int port, StatsEventBus bus) =>
        new(
            Options.Create(new StatsApiOptions { Port = port }),
            bus,
            NullLogger<StatsApiClient>.Instance);

    [Fact]
    public async Task RunAsync_publishes_each_line_to_the_bus()
    {
        // Payloads must match the typed event schemas (GoalScoredEvent, BallHitEvent); a shape mismatch
        // would cause StatsEventParser to throw JsonException, the client to swallow+log, and this
        // subscriber would block forever on a channel that never receives anything.
        var lines = new[]
        {
            """{"Event":"GoalScored","MatchGuid":"x","Data":{"GoalSpeed":1500,"GoalTime":60,"ImpactLocation":{"X":0,"Y":0,"Z":0},"Scorer":{"Name":"P","Shortcut":1,"TeamNum":0}}}""",
            """{"Event":"BallHit","MatchGuid":"x","Data":{"Players":[{"Name":"P","Shortcut":1,"TeamNum":0}],"Ball":{"PreHitSpeed":0,"PostHitSpeed":1500,"Location":{"X":1,"Y":2,"Z":3}}}}""",
        };
        await using var server = FakeStatsApiServer.Start(lines);
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();
        var client = BuildClient(server.Port, bus);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }   // server closed — fine

        // Bind reads to the same CT so a future schema-mismatch regression fails loudly with OCE
        // instead of hanging the test host.
        var evt1 = await reader.ReadAsync(cts.Token);
        var evt2 = await reader.ReadAsync(cts.Token);
        Assert.IsType<GoalScoredEvent>(evt1);
        Assert.IsType<BallHitEvent>(evt2);
    }

    [Fact]
    public async Task RunAsync_throws_SocketException_on_connect_failure()
    {
        // Use a definitely-unbound port (49099 has no listener).
        // No cancellation timeout — connection refused is instant on loopback.
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var client = BuildClient(port: 49099, bus);

        await Assert.ThrowsAnyAsync<SocketException>(() => client.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_skips_brace_balanced_but_malformed_objects_and_continues()
    {
        // The framer only knows how to find balanced { ... } boundaries — it can't recover from a
        // structurally-broken object that is missing its closing brace. So a realistic "skipped" message
        // is one whose braces ARE balanced but whose contents won't parse as JSON.
        var lines = new[]
        {
            """{"Event":"GoalScored","MatchGuid":"x","Data":{"GoalSpeed":1500,"GoalTime":60,"ImpactLocation":{"X":0,"Y":0,"Z":0},"Scorer":{"Name":"P","Shortcut":1,"TeamNum":0}}}""",
            "{not_valid_json}",
            """{"Event":"BallHit","MatchGuid":"x","Data":{"Players":[{"Name":"P","Shortcut":1,"TeamNum":0}],"Ball":{"PreHitSpeed":0,"PostHitSpeed":1500,"Location":{"X":1,"Y":2,"Z":3}}}}""",
        };
        await using var server = FakeStatsApiServer.Start(lines);
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();
        var client = BuildClient(server.Port, bus);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (IOException) { /* server closed — fine */ }

        // Bind reads to the same CT so a future schema-mismatch regression fails loudly with OCE
        // instead of hanging the test host.
        var evt1 = await reader.ReadAsync(cts.Token);
        var evt2 = await reader.ReadAsync(cts.Token);
        Assert.IsType<GoalScoredEvent>(evt1);
        Assert.IsType<BallHitEvent>(evt2);   // BallHit followed the malformed object
    }

    [Fact]
    public async Task RunAsync_handles_real_wire_shape_no_terminator_and_escaped_Data_string()
    {
        // Mirror exactly what the live plugin sends: complete JSON objects with NO newline between them,
        // and Data as an escaped JSON string (the double-encoding observed in the trace).
        var lines = new[]
        {
            """{"Event":"GoalScored","MatchGuid":"x","Data":"{\"GoalSpeed\":1834.5,\"GoalTime\":127.5,\"ImpactLocation\":{\"X\":0,\"Y\":-2944,\"Z\":320},\"Scorer\":{\"Name\":\"P\",\"Shortcut\":1,\"TeamNum\":0}}"}""",
            """{"Event":"BallHit","MatchGuid":"x","Data":"{\"Players\":[{\"Name\":\"P\",\"Shortcut\":1,\"TeamNum\":0}],\"Ball\":{\"PreHitSpeed\":0,\"PostHitSpeed\":1500,\"Location\":{\"X\":1,\"Y\":2,\"Z\":3}}}"}""",
        };
        await using var server = FakeStatsApiServer.Start(lines, lineTerminator: string.Empty);
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();
        var client = BuildClient(server.Port, bus);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (IOException) { /* server closed — fine */ }

        var goal = Assert.IsType<GoalScoredEvent>(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("P", goal.Scorer.Name);
        Assert.Equal(1834.5, goal.GoalSpeed);

        var hit = Assert.IsType<BallHitEvent>(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("P", hit.Players[0].Name);
        Assert.Equal(1500, hit.Ball.PostHitSpeed);
    }

    [Fact]
    public async Task RunAsync_returns_when_stream_ends_cleanly()
    {
        await using var server = FakeStatsApiServer.Start(["""{"Event":"GoalScored","MatchGuid":"x","Data":{"Scorer":"P","Team":0,"BlueScore":1,"OrangeScore":0}}"""]);
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var client = BuildClient(server.Port, bus);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Should complete without throwing once the fake server closes the connection.
        await client.RunAsync(cts.Token);
    }

    [Fact]
    public async Task RunAsync_in_trace_mode_reads_raw_bytes_without_publishing_events()
    {
        var lines = new[]
        {
            """{"Event":"GoalScored","MatchGuid":"x","Data":{"Scorer":"P","Team":0,"BlueScore":1,"OrangeScore":0}}""",
            """{"Event":"BallHit","MatchGuid":"x","Data":{"Player":"P","BallSpeed":1500}}""",
        };
        await using var server = FakeStatsApiServer.Start(lines);
        using var bus = new StatsEventBus(NullLogger<StatsEventBus>.Instance);
        var reader = bus.Subscribe();
        var client = new StatsApiClient(
            Options.Create(new StatsApiOptions { Port = server.Port, TraceMode = true }),
            bus,
            NullLogger<StatsApiClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Trace loop returns when the fake server closes the connection.
        await client.RunAsync(cts.Token);

        // Trace mode does not publish to the bus.
        Assert.False(reader.TryRead(out _));
    }
}
