namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Services;
using Xunit;

/// <summary>
/// Verifies the kickoff-phantom detector. Rocket League's Stats API emits a second
/// GoalScored event ~5-15s after each real goal in ranked matches — same MatchGuid,
/// empty scorer, GoalSpeed=0, GoalTime=0. We must suppress these or the dashboard
/// will double-count goals. Pattern observed in real captures (e.g. log lines
/// 382-383, 467-468, 592-593 of rl-stats-2026-05-01.jsonl).
/// </summary>
public sealed class KickoffPhantomFilterTests
{
    [Fact]
    public void Real_goal_with_named_scorer_is_not_a_phantom()
    {
        var evt = new GoalScoredEvent
        {
            Scorer = new PlayerRef("Hellcat", Shortcut: 1, TeamNum: 0),
            GoalSpeed = 2104,
            GoalTime = 132,
        };

        Assert.False(LiveMatchProjector.IsKickoffPhantom(evt));
    }

    [Fact]
    public void Phantom_with_empty_scorer_name_is_detected()
    {
        var evt = new GoalScoredEvent
        {
            Scorer = new PlayerRef(string.Empty, Shortcut: 0, TeamNum: 0),
            GoalSpeed = 0,
            GoalTime = 0,
        };

        Assert.True(LiveMatchProjector.IsKickoffPhantom(evt));
    }

    [Fact]
    public void Goal_with_zero_speed_but_real_scorer_is_not_a_phantom()
    {
        // Edge case: a wall-clip or weird scoring scenario can have GoalSpeed=0 with
        // a real scorer attached. We never filter on speed alone.
        var evt = new GoalScoredEvent
        {
            Scorer = new PlayerRef("Hellcat", Shortcut: 1, TeamNum: 0),
            GoalSpeed = 0,
            GoalTime = 132,
        };

        Assert.False(LiveMatchProjector.IsKickoffPhantom(evt));
    }

    [Fact]
    public void Team_attributed_own_goal_with_empty_scorer_but_real_speed_is_not_a_phantom()
    {
        // When a team scores on themselves and no opponent ever touched the ball, RL
        // emits a goal with an empty scorer (no player attribution) but a real impact
        // speed. We must NOT treat this as a phantom — the goal really happened.
        var evt = new GoalScoredEvent
        {
            Scorer = new PlayerRef(string.Empty, Shortcut: 0, TeamNum: 0),
            GoalSpeed = 78.5,
            GoalTime = 95,
        };

        Assert.False(LiveMatchProjector.IsKickoffPhantom(evt));
    }
}
