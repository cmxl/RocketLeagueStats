namespace RocketLeagueStats.WebApi.Services.Recap;

using RocketLeagueStats.WebApi.Contracts;

internal static class PlayerTallyAggregator
{
    public static PlayerStatsRowDto[] Aggregate(
        IReadOnlyCollection<PlayerRefDto> players,
        IReadOnlyCollection<GoalDto> goals,
        IReadOnlyCollection<StatfeedDto> statfeeds,
        bool markMvp)
    {
        var rows = new Dictionary<int, MutableRow>(players.Count);
        foreach (var p in players)
        {
            rows[p.Shortcut] = new MutableRow(p);
        }

        foreach (var g in goals)
        {
            if (rows.TryGetValue(g.Scorer.Shortcut, out var scorerRow))
            {
                scorerRow.Goals++;
                scorerRow.Shots++;
                if (g.GoalSpeedUuPerSec > scorerRow.FastestGoalSpeed)
                {
                    scorerRow.FastestGoalSpeed = g.GoalSpeedUuPerSec;
                }
            }

            if (g.Assister is not null && rows.TryGetValue(g.Assister.Shortcut, out var assistRow))
            {
                assistRow.Assists++;
            }
        }

        foreach (var s in statfeeds)
        {
            if (rows.TryGetValue(s.MainTarget.Shortcut, out var main))
            {
                switch (s.Type)
                {
                    case StatfeedType.Save:
                        main.Saves++;
                        break;
                    case StatfeedType.EpicSave:
                        main.EpicSaves++;
                        break;
                    case StatfeedType.Demolish:
                        main.DemosInflicted++;
                        break;
                    case StatfeedType.Other:
                    case StatfeedType.Hattrick:
                    case StatfeedType.MvpHattrick:
                    case StatfeedType.Savior:
                    case StatfeedType.BicycleHit:
                    case StatfeedType.Damage:
                    case StatfeedType.UltraDamage:
                    case StatfeedType.AerialGoal:
                    case StatfeedType.BackwardsGoal:
                    case StatfeedType.OvertimeGoal:
                    case StatfeedType.BicycleGoal:
                    case StatfeedType.LongGoal:
                    case StatfeedType.PoolShot:
                    case StatfeedType.Mvp:
                    case StatfeedType.Win:
                    default:
                        // Display-only event categories — surfaced in the timeline but not
                        // counted in the per-player tally (those are derived from goals/saves
                        // and the existing demolish/save buckets).
                        break;
                }
            }

            if (s.Type == StatfeedType.Demolish && s.SecondaryTarget is not null
                && rows.TryGetValue(s.SecondaryTarget.Shortcut, out var victim))
            {
                victim.DemosTaken++;
            }
        }

        var output = rows.Values.Select(r => r.ToDto(isMvp: false)).ToArray();

        if (markMvp && output.Length > 0)
        {
            var maxScore = output.Max(r => r.MvpScore);
            if (maxScore > 0)
            {
                for (var i = 0; i < output.Length; i++)
                {
                    if (Math.Abs(output[i].MvpScore - maxScore) < 0.001)
                    {
                        output[i] = output[i] with { IsMvp = true };
                        break;
                    }
                }
            }
        }

        return [.. output.OrderByDescending(r => r.IsMvp).ThenByDescending(r => r.MvpScore)];
    }

    private sealed class MutableRow(PlayerRefDto player)
    {
        public PlayerRefDto Player { get; } = player;

        public int Goals { get; set; }

        public int Assists { get; set; }

        public int Saves { get; set; }

        public int EpicSaves { get; set; }

        public int Shots { get; set; }

        public int DemosInflicted { get; set; }

        public int DemosTaken { get; set; }

        public int CrossbarHits { get; set; }

        public double FastestGoalSpeed { get; set; }

        public PlayerStatsRowDto ToDto(bool isMvp) => new(
            Player: this.Player,
            Goals: this.Goals,
            Assists: this.Assists,
            Saves: this.Saves,
            EpicSaves: this.EpicSaves,
            Shots: this.Shots,
            DemosInflicted: this.DemosInflicted,
            DemosTaken: this.DemosTaken,
            CrossbarHits: this.CrossbarHits,
            FastestGoalSpeedUuPerSec: this.FastestGoalSpeed,
            MvpScore: this.ComputeMvpScore(),
            IsMvp: isMvp);

        private double ComputeMvpScore() =>
            (this.Goals * 3.0) + (this.Assists * 2.0) + (this.Saves * 1.5) +
            (this.Shots * 0.5) + (this.EpicSaves * 2.0) - (this.DemosTaken * 0.5);
    }
}
