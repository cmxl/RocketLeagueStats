namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class LiveMatchStateTests
{
    private static MatchHeaderDto SampleHeader() => new(
        MatchId: "m1",
        StartedAt: DateTime.UtcNow,
        Type: MatchType.Casual,
        PlaylistRaw: "Casual2v2",
        BluePlayers: [new PlayerRefDto("Blue1", 1, "blue")],
        OrangePlayers: [new PlayerRefDto("Orange1", 2, "orange")],
        ArenaName: null);

    private static GoalDto SampleGoal(string team)
    {
        var scorer = team == "blue"
            ? new PlayerRefDto("Blue1", 1, "blue")
            : new PlayerRefDto("Orange1", 2, "orange");
        return new GoalDto(
            Id: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            MatchClockSeconds: 60,
            Scorer: scorer,
            Assister: null,
            GoalSpeedUuPerSec: 1500,
            ImpactLocation: new Vec3Dto(0, 0, 0),
            BlueScoreAfter: 0,
            OrangeScoreAfter: 0,
            SecondsSinceLastGoal: null);
    }

    [Fact]
    public void Idle_initially()
    {
        var state = new LiveMatchState();
        Assert.Equal(MatchPhase.Idle, state.Phase);
        Assert.Null(state.CurrentMatch);
    }

    [Fact]
    public void BeginMatch_transitions_to_live()
    {
        var state = new LiveMatchState();
        var header = SampleHeader();
        state.BeginMatch(header);
        Assert.Equal(MatchPhase.Live, state.Phase);
        Assert.Equal(header.MatchId, state.CurrentMatch!.MatchId);
        Assert.Equal(0, state.BlueScore);
        Assert.Equal(0, state.OrangeScore);
    }

    [Fact]
    public void Goal_increments_team_score_and_appends_to_recent()
    {
        var state = new LiveMatchState();
        state.BeginMatch(SampleHeader());
        var goal = SampleGoal("blue");
        state.AppendGoal(goal);
        Assert.Equal(1, state.BlueScore);
        Assert.Equal(0, state.OrangeScore);
        Assert.Single(state.RecentGoals);
    }

    [Fact]
    public void RecentGoals_caps_at_8_newest_first()
    {
        var state = new LiveMatchState();
        state.BeginMatch(SampleHeader());
        for (var i = 0; i < 10; i++)
        {
            state.AppendGoal(SampleGoal("blue"));
        }

        Assert.Equal(8, state.RecentGoals.Count);
    }

    [Fact]
    public void EndMatch_returns_to_idle_with_summary()
    {
        var state = new LiveMatchState();
        state.BeginMatch(SampleHeader());
        state.AppendGoal(SampleGoal("blue"));
        var summary = state.EndMatch();
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.BlueScore);
        Assert.Equal(MatchPhase.Idle, state.Phase);
        Assert.Null(state.CurrentMatch);
    }

    [Fact]
    public void Snapshot_returns_current_LiveStateDto()
    {
        var state = new LiveMatchState();
        state.BeginMatch(SampleHeader());
        state.AppendGoal(SampleGoal("orange"));

        var dto = state.ToLiveStateDto();
        Assert.Equal(MatchPhase.Live, dto.Phase);
        Assert.Equal(0, dto.BlueScore);
        Assert.Equal(1, dto.OrangeScore);
    }

    [Fact]
    public void UpdateRoster_replaces_player_arrays_without_resetting_scores_or_feeds()
    {
        var state = new LiveMatchState();
        state.BeginMatch(SampleHeader());
        state.AppendGoal(SampleGoal("blue"));    // blueScore = 1
        state.AppendGoal(SampleGoal("blue"));    // blueScore = 2
        Assert.Equal(2, state.BlueScore);
        Assert.Equal(2, state.RecentGoals.Count);

        var newBlue = new[] { new PlayerRefDto("Hellcat", 1, "blue"), new PlayerRefDto("Sub", 2, "blue") };
        var newOrange = new[] { new PlayerRefDto("Stink", 3, "orange") };
        var updated = state.UpdateRoster(newBlue, newOrange);

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.BluePlayers.Length);
        Assert.Single(updated.OrangePlayers);
        // Scores and feeds preserved:
        Assert.Equal(2, state.BlueScore);
        Assert.Equal(2, state.RecentGoals.Count);
    }

    [Fact]
    public void UpdateRoster_returns_null_when_no_match_active()
    {
        var state = new LiveMatchState();
        var result = state.UpdateRoster([], []);
        Assert.Null(result);
    }
}
