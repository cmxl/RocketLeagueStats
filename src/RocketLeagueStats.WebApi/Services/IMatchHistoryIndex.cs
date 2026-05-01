namespace RocketLeagueStats.WebApi.Services;

using RocketLeagueStats.WebApi.Contracts;

public interface IMatchHistoryIndex
{
    public void BeginMatch(MatchHeaderDto header);

    public void AppendGoal(string matchId, GoalDto goal);

    public void AppendStatfeed(string matchId, StatfeedDto statfeed);

    public void CompleteMatch(string matchId, MatchSummaryDto summary);

    public IReadOnlyList<MatchSummaryDto> GetMatches(HistoryFilter filter);

    public MatchRecapDto? GetRecap(string matchId);
}
