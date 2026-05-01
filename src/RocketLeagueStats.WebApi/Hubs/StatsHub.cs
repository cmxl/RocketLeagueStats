namespace RocketLeagueStats.WebApi.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// SignalR hub for live stats. Broadcast-only — no client→server methods in v1.
/// Clients bootstrap their state via HTTP GET /api/state, then listen for incremental updates here.
/// </summary>
public sealed class StatsHub : Hub<IStatsHubClient>
{
    // Intentionally empty: the hub exists purely to expose the IStatsHubClient broadcast surface.
    // Server-side broadcasts happen via IHubContext<StatsHub, IStatsHubClient>, injected into LiveMatchProjector.
}
