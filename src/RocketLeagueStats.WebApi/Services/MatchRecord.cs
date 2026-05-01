namespace RocketLeagueStats.WebApi.Services;

using RocketLeagueStats.WebApi.Contracts;

internal sealed class MatchRecord
{
    public required MatchHeaderDto Header { get; init; }

    public List<GoalDto> Goals { get; } = [];

    public List<StatfeedDto> Statfeeds { get; } = [];

    public MatchSummaryDto? Summary { get; set; }

    public bool IsCompleted => this.Summary is not null;
}
