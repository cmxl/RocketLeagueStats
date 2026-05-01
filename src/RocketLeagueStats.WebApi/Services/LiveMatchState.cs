namespace RocketLeagueStats.WebApi.Services;

using System.Threading;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services.Recap;

internal sealed class LiveMatchState
{
    private readonly Lock syncLock = new();

    // IDE0032 suppressed: backing fields are required for lock(syncLock)-guarded writes.
#pragma warning disable IDE0032
    private MatchHeaderDto? activeMatch;
    private int blueGoals;
    private int orangeGoals;
#pragma warning restore IDE0032

    private int? elapsedSeconds;
    private DateTime? lastGoalTimestamp;
    private bool isGameConnected = true;
    private DateTime? lastEventAt;
    private readonly List<GoalDto> recentGoals = new(capacity: 16);
    private readonly List<StatfeedDto> recentStatfeeds = new(capacity: 16);
    private readonly List<GoalDto> goalsThisMatch = [];
    private readonly List<StatfeedDto> statfeedsThisMatch = [];

    public MatchPhase Phase => this.activeMatch is null ? MatchPhase.Idle : MatchPhase.Live;

    public MatchHeaderDto? CurrentMatch => this.activeMatch;

    public int BlueScore => this.blueGoals;

    public int OrangeScore => this.orangeGoals;

    public IReadOnlyList<GoalDto> RecentGoals => this.recentGoals;

    public IReadOnlyList<StatfeedDto> RecentStatfeeds => this.recentStatfeeds;

    public PlayerStatsRowDto[] CurrentPlayerStats()
    {
        if (this.activeMatch is null)
        {
            return [];
        }

        PlayerRefDto[] allPlayers = [.. this.activeMatch.BluePlayers, .. this.activeMatch.OrangePlayers];
        return PlayerTallyAggregator.Aggregate(allPlayers, this.goalsThisMatch, this.statfeedsThisMatch, markMvp: false);
    }

    public void BeginMatch(MatchHeaderDto header)
    {
        lock (this.syncLock)
        {
            this.activeMatch = header;
            this.blueGoals = 0;
            this.orangeGoals = 0;
            this.elapsedSeconds = null;
            this.lastGoalTimestamp = null;
            this.recentGoals.Clear();
            this.recentStatfeeds.Clear();
            this.goalsThisMatch.Clear();
            this.statfeedsThisMatch.Clear();
        }
    }

    /// <summary>
    /// Replace the current match's roster (BluePlayers/OrangePlayers) without resetting scores
    /// or event feeds. Returns the updated header so the projector can re-broadcast it. Returns
    /// null if no match is active.
    /// </summary>
    public MatchHeaderDto? UpdateRoster(PlayerRefDto[] bluePlayers, PlayerRefDto[] orangePlayers)
    {
        lock (this.syncLock)
        {
            if (this.activeMatch is null)
            {
                return null;
            }

            this.activeMatch = this.activeMatch with
            {
                BluePlayers = bluePlayers,
                OrangePlayers = orangePlayers,
            };
            return this.activeMatch;
        }
    }

    public void AppendGoal(GoalDto goal)
    {
        lock (this.syncLock)
        {
            if (goal.Scorer.Team == "blue")
            {
                this.blueGoals++;
            }
            else if (goal.Scorer.Team == "orange")
            {
                this.orangeGoals++;
            }

            this.lastGoalTimestamp = goal.Timestamp;
            this.lastEventAt = DateTime.UtcNow;

            var withScores = goal with { BlueScoreAfter = this.blueGoals, OrangeScoreAfter = this.orangeGoals };
            this.recentGoals.Insert(0, withScores);
            if (this.recentGoals.Count > 8)
            {
                this.recentGoals.RemoveAt(this.recentGoals.Count - 1);
            }

            this.goalsThisMatch.Add(withScores);
        }
    }

    public void AppendStatfeed(StatfeedDto statfeed)
    {
        lock (this.syncLock)
        {
            this.lastEventAt = DateTime.UtcNow;
            this.recentStatfeeds.Insert(0, statfeed);
            if (this.recentStatfeeds.Count > 8)
            {
                this.recentStatfeeds.RemoveAt(this.recentStatfeeds.Count - 1);
            }

            this.statfeedsThisMatch.Add(statfeed);
        }
    }

    public void UpdateClock(int seconds)
    {
        lock (this.syncLock)
        {
            this.elapsedSeconds = seconds;
        }
    }

    public void SetGameConnected(bool connected)
    {
        lock (this.syncLock)
        {
            this.isGameConnected = connected;
        }
    }

    public MatchSummaryDto? EndMatch()
    {
        lock (this.syncLock)
        {
            if (this.activeMatch is null)
            {
                return null;
            }

            PlayerRefDto[] allPlayers = [.. this.activeMatch.BluePlayers, .. this.activeMatch.OrangePlayers];
            var stats = PlayerTallyAggregator.Aggregate(allPlayers, this.goalsThisMatch, this.statfeedsThisMatch, markMvp: true);
            var mvp = stats.FirstOrDefault(r => r.IsMvp)?.Player;
            var fastestGoal = this.goalsThisMatch
                .OrderByDescending(g => g.GoalSpeedUuPerSec)
                .FirstOrDefault();

            var summary = new MatchSummaryDto(
                MatchId: this.activeMatch.MatchId,
                StartedAt: this.activeMatch.StartedAt,
                EndedAt: DateTime.UtcNow,
                DurationSeconds: this.elapsedSeconds ?? 0,
                Type: this.activeMatch.Type,
                BlueScore: this.blueGoals,
                OrangeScore: this.orangeGoals,
                AllPlayers: allPlayers,
                Mvp: mvp,
                TotalGoals: this.goalsThisMatch.Count,
                FastestGoal: fastestGoal);

            this.activeMatch = null;
            this.elapsedSeconds = null;
            return summary;
        }
    }

    public LiveStateDto ToLiveStateDto()
    {
        lock (this.syncLock)
        {
            return new LiveStateDto(
                Phase: this.Phase,
                CurrentMatch: this.activeMatch,
                CurrentMatchClockSeconds: this.elapsedSeconds,
                BlueScore: this.blueGoals,
                OrangeScore: this.orangeGoals,
                PlayerStats: this.CurrentPlayerStats(),
                RecentGoals: [.. this.recentGoals],
                RecentStatfeeds: [.. this.recentStatfeeds],
                LastGoalAt: this.lastGoalTimestamp,
                Connection: new ConnectionStateDto(this.isGameConnected, this.lastEventAt));
        }
    }
}
