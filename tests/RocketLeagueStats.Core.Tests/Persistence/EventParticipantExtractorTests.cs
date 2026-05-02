namespace RocketLeagueStats.Core.Tests.Persistence;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.Core.Persistence;

public sealed class EventParticipantExtractorTests
{
    private static readonly PlayerRef Tobi = new("Tobi", 1, 0);
    private static readonly PlayerRef Jay = new("Jay", 2, 1);
    private static readonly PlayerRef Vex = new("Vex", 3, 0);

    [Fact]
    public void GoalScored_WithScorerOnly_EmitsScorer()
    {
        var evt = new GoalScoredEvent { Scorer = Tobi, ImpactLocation = default };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Single(participants);
        Assert.Equal(new ExtractedParticipant("Tobi", 1, 0, ParticipantRoles.Scorer), participants[0]);
    }

    [Fact]
    public void GoalScored_WithAssistAndLastTouch_EmitsAllThree()
    {
        var evt = new GoalScoredEvent
        {
            Scorer = Tobi,
            Assister = Jay,
            BallLastTouch = new BallLastTouchInfo(Vex, 1234.5),
            ImpactLocation = default,
        };

        var roles = EventParticipantExtractor.Extract(evt).Select(p => p.Role).ToList();

        Assert.Equal(
            [ParticipantRoles.Scorer, ParticipantRoles.Assister, ParticipantRoles.BallLastTouch],
            roles);
    }

    private static readonly string[] ExpectedBallHitNames = ["Tobi", "Jay", "Vex"];

    [Fact]
    public void BallHit_EmitsAllPlayersWithBallHitRole()
    {
        var evt = new BallHitEvent { Players = [Tobi, Jay, Vex] };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Equal(3, participants.Count);
        Assert.All(participants, p => Assert.Equal(ParticipantRoles.BallHit, p.Role));
        Assert.Equal(ExpectedBallHitNames, participants.Select(p => p.PlayerName));
    }

    [Fact]
    public void Statfeed_WithMainTargetOnly_EmitsMainTarget()
    {
        var evt = new StatfeedEvent { StatName = "Demolish", Type = "Default", MainTarget = Tobi };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Single(participants);
        Assert.Equal(ParticipantRoles.MainTarget, participants[0].Role);
    }

    [Fact]
    public void Statfeed_WithSecondaryTarget_EmitsBoth()
    {
        var evt = new StatfeedEvent
        {
            StatName = "Demolish",
            Type = "Default",
            MainTarget = Tobi,
            SecondaryTarget = Jay,
        };

        var roles = EventParticipantExtractor.Extract(evt).Select(p => p.Role).ToList();

        Assert.Equal([ParticipantRoles.MainTarget, ParticipantRoles.SecondaryTarget], roles);
    }

    [Fact]
    public void CrossbarHit_WithLastTouch_EmitsBallLastTouch()
    {
        var evt = new CrossbarHitEvent
        {
            BallLastTouch = new BallLastTouchInfo(Tobi, 999.0),
        };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Single(participants);
        Assert.Equal(ParticipantRoles.BallLastTouch, participants[0].Role);
    }

    [Fact]
    public void CrossbarHit_WithoutLastTouch_EmitsNothing()
    {
        var evt = new CrossbarHitEvent();

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Empty(participants);
    }

    [Fact]
    public void ClockUpdated_EmitsNothing()
    {
        var evt = new ClockUpdatedSecondsEvent { TimeSeconds = 30 };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Empty(participants);
    }

    [Fact]
    public void MatchEnded_EmitsNothing()
    {
        var evt = new MatchEndedEvent { WinnerTeamNum = 1 };

        var participants = EventParticipantExtractor.Extract(evt).ToList();

        Assert.Empty(participants);
    }
}
