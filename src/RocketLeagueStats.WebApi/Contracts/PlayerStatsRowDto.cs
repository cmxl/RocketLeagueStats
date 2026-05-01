namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Per-player aggregated stats in a match.</summary>
public sealed record PlayerStatsRowDto(
    PlayerRefDto Player,
    int Goals,
    int Assists,
    int Saves,
    int EpicSaves,
    int Shots,
    int DemosInflicted,
    int DemosTaken,
    int CrossbarHits,
    double FastestGoalSpeedUuPerSec,
    double MvpScore,
    bool IsMvp);
