namespace RocketLeagueStats.WebApi.Services.Recap;

using RocketLeagueStats.WebApi.Contracts;

internal static class RecapBuilder
{
    public static MatchRecapDto Build(MatchRecord record)
    {
        // Summary.AllPlayers carries the lazily-discovered roster captured at match end;
        // record.Header is sealed at MatchInitialized when RL hasn't surfaced the roster yet
        // (see LiveMatchProjector.MaybeUpdateRosterAsync — it only updates LiveMatchState).
        var playerStats = PlayerTallyAggregator.Aggregate(
            record.Summary!.AllPlayers,
            record.Goals,
            record.Statfeeds,
            markMvp: true);

        return new MatchRecapDto(
            Summary: record.Summary!,
            Goals: [.. record.Goals],
            Statfeeds: [.. record.Statfeeds],
            PlayerStats: playerStats,
            TimeBetweenGoalsSeconds: ComputeTimeBetweenGoals(record.Goals),
            Flow: BuildFlow(record));
    }

    private static int[] ComputeTimeBetweenGoals(List<GoalDto> goals)
    {
        if (goals.Count == 0)
        {
            return [];
        }

        var sorted = goals.OrderBy(g => g.MatchClockSeconds).ToList();
        var result = new int[sorted.Count];
        result[0] = sorted[0].MatchClockSeconds;
        for (var i = 1; i < sorted.Count; i++)
        {
            result[i] = sorted[i].MatchClockSeconds - sorted[i - 1].MatchClockSeconds;
        }

        return result;
    }

    private static GameFlowDto BuildFlow(MatchRecord record)
    {
        if (record.Goals.Count == 0)
        {
            return new GameFlowDto(
                TimestampSeconds: [0, record.Summary!.DurationSeconds],
                BlueScoreAtStep: [0, record.Summary.BlueScore],
                OrangeScoreAtStep: [0, record.Summary.OrangeScore]);
        }

        var sorted = record.Goals.OrderBy(g => g.MatchClockSeconds).ToList();
        var times = new List<int> { 0 };
        var blue = new List<int> { 0 };
        var orange = new List<int> { 0 };
        var b = 0;
        var o = 0;
        foreach (var g in sorted)
        {
            if (g.Scorer.Team == "blue")
            {
                b++;
            }
            else if (g.Scorer.Team == "orange")
            {
                o++;
            }

            times.Add(g.MatchClockSeconds);
            blue.Add(b);
            orange.Add(o);
        }

        times.Add(record.Summary!.DurationSeconds);
        blue.Add(b);
        orange.Add(o);

        return new GameFlowDto([.. times], [.. blue], [.. orange]);
    }
}
