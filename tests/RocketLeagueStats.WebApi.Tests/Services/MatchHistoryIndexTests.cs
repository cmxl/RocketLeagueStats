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

    [Fact]
    public void GetRecap_populates_player_stats_when_header_roster_was_empty_at_init()
    {
        // Reproduces the production data flow: RL's MatchInitialized event doesn't carry a
        // roster, so LiveMatchProjector registers a header with empty BluePlayers/OrangePlayers.
        // Players are discovered lazily from goal/statfeed events and only end up in
        // Summary.AllPlayers (via LiveMatchState.EndMatch). If the recap is built from the
        // header, playerStats is empty and the Angular table renders no rows.
        var index = new MatchHistoryIndex();
        var emptyHeader = new MatchHeaderDto(
            MatchId: "match-empty-roster",
            StartedAt: DateTime.UtcNow,
            Type: MatchType.Casual,
            PlaylistRaw: string.Empty,
            BluePlayers: [],
            OrangePlayers: [],
            ArenaName: null);
        var hellcat = new PlayerRefDto("Hellcat", 1, "blue");
        var stink = new PlayerRefDto("Stink", 2, "orange");
        var goal = new GoalDto(
            Id: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            MatchClockSeconds: 60,
            Scorer: hellcat,
            Assister: null,
            GoalSpeedUuPerSec: 1500,
            ImpactLocation: new Vec3Dto(0, 0, 0),
            BlueScoreAfter: 1,
            OrangeScoreAfter: 0,
            SecondsSinceLastGoal: 60);

        index.BeginMatch(emptyHeader);
        index.AppendGoal(emptyHeader.MatchId, goal);
        index.CompleteMatch(emptyHeader.MatchId, new MatchSummaryDto(
            MatchId: emptyHeader.MatchId,
            StartedAt: emptyHeader.StartedAt,
            EndedAt: emptyHeader.StartedAt.AddMinutes(5),
            DurationSeconds: 300,
            Type: emptyHeader.Type,
            BlueScore: 1,
            OrangeScore: 0,
            AllPlayers: [hellcat, stink],
            Mvp: hellcat,
            TotalGoals: 1,
            FastestGoal: goal));

        var recap = index.GetRecap(emptyHeader.MatchId);
        Assert.NotNull(recap);
        Assert.Equal(2, recap!.PlayerStats.Length);
        var hellcatRow = recap.PlayerStats.Single(r => r.Player.Shortcut == 1);
        Assert.Equal(1, hellcatRow.Goals);
        Assert.True(hellcatRow.IsMvp);
    }
}
