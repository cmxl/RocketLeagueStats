namespace RocketLeagueStats.WebApi.Services;

public sealed record HistoryFilter(
    bool IncludeTraining,
    bool IncludeFreePlay,
    DateTime? From,
    DateTime? To,
    HistorySort Sort)
{
    public static HistoryFilter Default { get; } = new(
        IncludeTraining: false,
        IncludeFreePlay: false,
        From: null,
        To: null,
        Sort: HistorySort.MostRecent);
}

public enum HistorySort
{
    MostRecent,
    HighestScoring,
}
