namespace RocketLeagueStats.WebApi.Services;

using System.Collections.Concurrent;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services.Recap;

internal sealed class MatchHistoryIndex : IMatchHistoryIndex
{
    private readonly ConcurrentDictionary<string, MatchRecord> records = new();

    public void BeginMatch(MatchHeaderDto header) =>
        this.records[header.MatchId] = new MatchRecord { Header = header };

    public void AppendGoal(string matchId, GoalDto goal)
    {
        if (this.records.TryGetValue(matchId, out var record))
        {
            record.Goals.Add(goal);
        }
    }

    public void AppendStatfeed(string matchId, StatfeedDto statfeed)
    {
        if (this.records.TryGetValue(matchId, out var record))
        {
            record.Statfeeds.Add(statfeed);
        }
    }

    public void CompleteMatch(string matchId, MatchSummaryDto summary)
    {
        if (this.records.TryGetValue(matchId, out var record))
        {
            record.Summary = summary;
        }
    }

    public IReadOnlyList<MatchSummaryDto> GetMatches(HistoryFilter filter)
    {
        var query = this.records.Values
            .Where(r => r.IsCompleted)
            .Select(r => r.Summary!)
            .Where(s => filter.IncludeTraining || s.Type != MatchType.Training)
            .Where(s => filter.IncludeFreePlay || s.Type != MatchType.FreePlay)
            .Where(s => filter.From is null || s.StartedAt >= filter.From)
            .Where(s => filter.To is null || s.EndedAt <= filter.To);

        query = filter.Sort switch
        {
            HistorySort.MostRecent => query.OrderByDescending(s => s.EndedAt),
            HistorySort.HighestScoring => query.OrderByDescending(s => s.TotalGoals),
            _ => query,
        };

        return [.. query];
    }

    public MatchRecapDto? GetRecap(string matchId)
    {
        if (!this.records.TryGetValue(matchId, out var record) || !record.IsCompleted)
        {
            return null;
        }

        return RecapBuilder.Build(record);
    }
}
