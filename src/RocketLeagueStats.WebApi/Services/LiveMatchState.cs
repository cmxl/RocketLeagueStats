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
    private readonly List<GoalDto> goals = [];
    private readonly List<StatfeedDto> statfeeds = [];

    public MatchPhase Phase => this.activeMatch is null ? MatchPhase.Idle : MatchPhase.Live;

    public MatchHeaderDto? CurrentMatch => this.activeMatch;

    public int BlueScore => this.blueGoals;

    public int OrangeScore => this.orangeGoals;

    /// <summary>All goals captured this match, newest first.</summary>
    public IReadOnlyList<GoalDto> Goals => this.goals;

    /// <summary>All statfeed events captured this match, newest first.</summary>
    public IReadOnlyList<StatfeedDto> Statfeeds => this.statfeeds;

    public PlayerStatsRowDto[] CurrentPlayerStats()
    {
        if (this.activeMatch is null)
        {
            return [];
        }

        PlayerRefDto[] allPlayers = [.. this.activeMatch.BluePlayers, .. this.activeMatch.OrangePlayers];
        return PlayerTallyAggregator.Aggregate(allPlayers, this.goals, this.statfeeds, markMvp: false);
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
            this.goals.Clear();
            this.statfeeds.Clear();
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

    /// <summary>
    /// Enriches the current match's header with full roster + team metadata + arena from the first
    /// MatchStateSnapshot tick. Returns the updated header so the projector can broadcast it, or
    /// null if no match is active. Existing roster entries are replaced (snapshot is authoritative);
    /// scores and event feeds are preserved.
    /// </summary>
    public MatchHeaderDto? EnrichFromSnapshot(
        PlayerRefDto[] bluePlayers,
        PlayerRefDto[] orangePlayers,
        TeamDto? blueTeam,
        TeamDto? orangeTeam,
        string? arenaName)
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
                BlueTeam = blueTeam,
                OrangeTeam = orangeTeam,
                ArenaName = arenaName ?? this.activeMatch.ArenaName,
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

            // Insert at index 0 so consumers see newest-first; the full match history is retained.
            var withScores = goal with { BlueScoreAfter = this.blueGoals, OrangeScoreAfter = this.orangeGoals };
            this.goals.Insert(0, withScores);
        }
    }

    public void AppendStatfeed(StatfeedDto statfeed)
    {
        lock (this.syncLock)
        {
            this.lastEventAt = DateTime.UtcNow;
            this.statfeeds.Insert(0, statfeed);
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
            var stats = PlayerTallyAggregator.Aggregate(allPlayers, this.goals, this.statfeeds, markMvp: true);
            var mvp = stats.FirstOrDefault(r => r.IsMvp)?.Player;
            var fastestGoal = this.goals
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
                TotalGoals: this.goals.Count,
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
                Goals: [.. this.goals],
                Statfeeds: [.. this.statfeeds],
                LastGoalAt: this.lastGoalTimestamp,
                Connection: new ConnectionStateDto(this.isGameConnected, this.lastEventAt));
        }
    }
}
