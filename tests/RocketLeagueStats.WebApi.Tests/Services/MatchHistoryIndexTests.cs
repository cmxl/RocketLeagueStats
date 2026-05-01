namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class MatchHistoryIndexTests
{
    private static MatchHeaderDto SampleHeader(string id = "match-1") => new(
        MatchId: id,
        StartedAt: DateTime.UtcNow,
        Type: MatchType.Casual,
        PlaylistRaw: "Casual2v2",
        BluePlayers: [new PlayerRefDto("Hellcat", 1, "blue")],
        OrangePlayers: [new PlayerRefDto("Stinkmaster", 2, "orange")],
        ArenaName: "Mannfield");

    private static MatchSummaryDto BuildSummary(MatchHeaderDto header) => new(
        MatchId: header.MatchId,
        StartedAt: header.StartedAt,
        EndedAt: header.StartedAt.AddMinutes(5),
        DurationSeconds: 300,
        Type: header.Type,
        BlueScore: 0,
        OrangeScore: 0,
        AllPlayers: [header.BluePlayers[0], header.OrangePlayers[0]],
        Mvp: null,
        TotalGoals: 0,
        FastestGoal: null);

    [Fact]
    public void New_index_is_empty()
    {
        var index = new MatchHistoryIndex();
        Assert.Empty(index.GetMatches(HistoryFilter.Default));
    }

    [Fact]
    public void BeginMatch_adds_in_progress_match_but_GetMatches_excludes_it()
    {
        var index = new MatchHistoryIndex();
        index.BeginMatch(SampleHeader());
        Assert.Empty(index.GetMatches(HistoryFilter.Default));
    }

    [Fact]
    public void CompleteMatch_makes_match_visible_in_history()
    {
        var index = new MatchHistoryIndex();
        var header = SampleHeader();
        index.BeginMatch(header);
        index.CompleteMatch(header.MatchId, BuildSummary(header));
        Assert.Single(index.GetMatches(HistoryFilter.Default));
    }

    [Fact]
    public void Default_filter_excludes_training_matches()
    {
        var index = new MatchHistoryIndex();
        var trainingHeader = SampleHeader("training-1") with { Type = MatchType.Training };
        index.BeginMatch(trainingHeader);
        index.CompleteMatch(trainingHeader.MatchId, BuildSummary(trainingHeader));

        var casualHeader = SampleHeader("casual-1");
        index.BeginMatch(casualHeader);
        index.CompleteMatch(casualHeader.MatchId, BuildSummary(casualHeader));

        var matches = index.GetMatches(HistoryFilter.Default);
        Assert.Single(matches);
        Assert.Equal("casual-1", matches[0].MatchId);
    }

    [Fact]
    public void Filter_can_include_training_matches()
    {
        var index = new MatchHistoryIndex();
        var trainingHeader = SampleHeader("training-1") with { Type = MatchType.Training };
        index.BeginMatch(trainingHeader);
        index.CompleteMatch(trainingHeader.MatchId, BuildSummary(trainingHeader));

        var matches = index.GetMatches(HistoryFilter.Default with { IncludeTraining = true });
        Assert.Single(matches);
    }

    [Fact]
    public void GetRecap_returns_null_for_unknown_match_id()
    {
        var index = new MatchHistoryIndex();
        Assert.Null(index.GetRecap("does-not-exist"));
    }

    [Fact]
    public void GetRecap_returns_recap_for_completed_match()
    {
        var index = new MatchHistoryIndex();
        var header = SampleHeader();
        index.BeginMatch(header);
        var summary = BuildSummary(header);
        index.CompleteMatch(header.MatchId, summary);

        var recap = index.GetRecap(header.MatchId);
        Assert.NotNull(recap);
        Assert.Equal(header.MatchId, recap!.Summary.MatchId);
    }
}
