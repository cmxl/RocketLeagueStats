namespace RocketLeagueStats.Core.Persistence;

using RocketLeagueStats.Core.Events;

public readonly record struct ExtractedParticipant(string PlayerName, int Shortcut, int TeamNum, string Role);

public static class EventParticipantExtractor
{
    public static IEnumerable<ExtractedParticipant> Extract(StatsEvent evt)
    {
        switch (evt)
        {
            case GoalScoredEvent goal:
                yield return ToParticipant(goal.Scorer, ParticipantRoles.Scorer);
                if (goal.Assister is { } assister)
                {
                    yield return ToParticipant(assister, ParticipantRoles.Assister);
                }
                if (goal.BallLastTouch is { } touch)
                {
                    yield return ToParticipant(touch.Player, ParticipantRoles.BallLastTouch);
                }
                break;

            case BallHitEvent hit:
                foreach (var p in hit.Players)
                {
                    yield return ToParticipant(p, ParticipantRoles.BallHit);
                }
                break;

            case StatfeedEvent stat:
                yield return ToParticipant(stat.MainTarget, ParticipantRoles.MainTarget);
                if (stat.SecondaryTarget is { } secondary)
                {
                    yield return ToParticipant(secondary, ParticipantRoles.SecondaryTarget);
                }
                break;

            case CrossbarHitEvent cross:
                if (cross.BallLastTouch is { } crossTouch)
                {
                    yield return ToParticipant(crossTouch.Player, ParticipantRoles.BallLastTouch);
                }
                break;

            default:
                yield break;
        }
    }

    private static ExtractedParticipant ToParticipant(PlayerRef player, string role) =>
        new(player.Name, player.Shortcut, player.TeamNum, role);
}
