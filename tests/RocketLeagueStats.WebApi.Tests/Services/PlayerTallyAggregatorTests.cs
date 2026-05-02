namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services.Recap;
using Xunit;

public sealed class PlayerTallyAggregatorTests
{
    private static readonly PlayerRefDto Hellcat = new("Hellcat", 1, "blue");
    private static readonly PlayerRefDto Sub = new("Sub", 2, "blue");
    private static readonly PlayerRefDto Stink = new("Stink", 3, "orange");

    [Fact]
    public void Empty_when_no_events()
    {
        var rows = PlayerTallyAggregator.Aggregate(
            players: [Hellcat, Stink],
            goals: [],
            statfeeds: [],
            markMvp: true);

        Assert.Equal(2, rows.Length);
        Assert.All(rows, r =>
        {
            Assert.Equal(0, r.Goals);
            Assert.Equal(0, r.Saves);
            Assert.False(r.IsMvp);
        });
    }

    [Fact]
    public void Counts_goals_assists_and_speeds()
    {
        var goal = SampleGoal(scorer: Hellcat, assister: Sub, speedUu: 2104);
        var rows = PlayerTallyAggregator.Aggregate(
            players: [Hellcat, Sub, Stink],
            goals: [goal],
            statfeeds: [],
            markMvp: false);

        var hell = rows.Single(r => r.Player.Shortcut == 1);
        var sub = rows.Single(r => r.Player.Shortcut == 2);
        Assert.Equal(1, hell.Goals);
        Assert.Equal(0, hell.Assists);
        Assert.Equal(2104, hell.FastestGoalSpeedUuPerSec);
        Assert.Equal(0, sub.Goals);
        Assert.Equal(1, sub.Assists);
    }

    [Fact]
    public void Counts_saves_demos_and_epic_saves()
    {
        StatfeedDto[] statfeeds =
        [
            SampleStatfeed(StatfeedType.Save, main: Hellcat, secondary: null),
            SampleStatfeed(StatfeedType.EpicSave, main: Hellcat, secondary: null),
            SampleStatfeed(StatfeedType.Demolish, main: Hellcat, secondary: Stink),
        ];

        var rows = PlayerTallyAggregator.Aggregate(
            players: [Hellcat, Stink],
            goals: [],
            statfeeds: statfeeds,
            markMvp: false);

        var hell = rows.Single(r => r.Player.Shortcut == 1);
        var stink = rows.Single(r => r.Player.Shortcut == 3);
        Assert.Equal(1, hell.Saves);
        Assert.Equal(1, hell.EpicSaves);
        Assert.Equal(1, hell.DemosInflicted);
        Assert.Equal(0, hell.DemosTaken);
        Assert.Equal(1, stink.DemosTaken);
    }

    [Fact]
    public void Computes_MvpScore_per_formula()
    {
        GoalDto[] goals =
        [
            SampleGoal(scorer: Hellcat, assister: null, speedUu: 1000),
            SampleGoal(scorer: Hellcat, assister: null, speedUu: 1500),
        ];
        StatfeedDto[] statfeeds =
        [
            SampleStatfeed(StatfeedType.Save, main: Hellcat, secondary: null),
            SampleStatfeed(StatfeedType.EpicSave, main: Hellcat, secondary: null),
        ];
        var rows = PlayerTallyAggregator.Aggregate(
            [Hellcat],
            goals,
            statfeeds,
            markMvp: false);

        var hell = rows.Single();
        // Formula: goals*3 + assists*2 + saves*1.5 + shots*0.5 + epicSaves*2 - demosTaken*0.5
        // 2*3 + 0*2 + 1*1.5 + 2*0.5 (shots == goals scored) + 1*2 - 0*0.5 = 6 + 1.5 + 1 + 2 = 10.5
        Assert.Equal(10.5, hell.MvpScore, precision: 2);
    }

    [Fact]
    public void Marks_highest_scorer_as_MVP_when_markMvp_true()
    {
        GoalDto[] goals =
        [
            SampleGoal(scorer: Hellcat, assister: null, speedUu: 1000),
            SampleGoal(scorer: Hellcat, assister: null, speedUu: 1500),
            SampleGoal(scorer: Stink, assister: null, speedUu: 800),
        ];
        var rows = PlayerTallyAggregator.Aggregate(
            [Hellcat, Stink],
            goals,
            [],
            markMvp: true);

        var hell = rows.Single(r => r.Player.Shortcut == 1);
        var stink = rows.Single(r => r.Player.Shortcut == 3);
        Assert.True(hell.IsMvp);
        Assert.False(stink.IsMvp);
    }

    private static GoalDto SampleGoal(PlayerRefDto scorer, PlayerRefDto? assister, double speedUu) => new(
        Id: Guid.NewGuid().ToString(),
        Timestamp: DateTime.UtcNow,
        MatchClockSeconds: 60,
        Scorer: scorer,
        Assister: assister,
        GoalSpeedUuPerSec: speedUu,
        ImpactLocation: new Vec3Dto(0, 0, 0),
        BlueScoreAfter: 0,
        OrangeScoreAfter: 0,
        SecondsSinceLastGoal: null);

    private static StatfeedDto SampleStatfeed(StatfeedType type, PlayerRefDto main, PlayerRefDto? secondary) => new(
        Timestamp: DateTime.UtcNow,
        MatchClockSeconds: 60,
        Type: type,
        DisplayName: type.ToString(),
        MainTarget: main,
        SecondaryTarget: secondary);
}
