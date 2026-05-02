namespace RocketLeagueStats.WebApi.Mapping;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;

internal static class EventMapper
{
    public static GoalDto ToDto(GoalScoredEvent evt, int matchClockSeconds, int? secondsSinceLastGoal) => new(
        Id: Guid.NewGuid().ToString(),
        Timestamp: DateTime.UtcNow,
        MatchClockSeconds: matchClockSeconds,
        Scorer: PlayerRefMapper.ToDto(evt.Scorer),
        Assister: evt.Assister is { } a ? PlayerRefMapper.ToDto(a) : null,
        GoalSpeedUuPerSec: evt.GoalSpeed,
        ImpactLocation: new Vec3Dto(evt.ImpactLocation.X, evt.ImpactLocation.Y, evt.ImpactLocation.Z),
        BlueScoreAfter: 0,
        OrangeScoreAfter: 0,
        SecondsSinceLastGoal: secondsSinceLastGoal);

    public static StatfeedDto ToDto(StatfeedEvent evt, int matchClockSeconds) => new(
        Timestamp: DateTime.UtcNow,
        MatchClockSeconds: matchClockSeconds,
        Type: ClassifyStatName(evt.StatName),
        DisplayName: !string.IsNullOrEmpty(evt.Type) ? evt.Type : evt.StatName,
        MainTarget: PlayerRefMapper.ToDto(evt.MainTarget),
        SecondaryTarget: evt.SecondaryTarget is { } s ? PlayerRefMapper.ToDto(s) : null);

    /// <summary>
    /// Maps RL's <c>EventName</c> (PascalCase wire identifier) to a stable enum bucket.
    /// Unrecognized names route to <see cref="StatfeedType.Other"/> — the verbatim
    /// display label still rides through on <see cref="StatfeedDto.DisplayName"/>.
    /// </summary>
    private static StatfeedType ClassifyStatName(string statName) => statName switch
    {
        "Save" => StatfeedType.Save,
        "EpicSave" => StatfeedType.EpicSave,
        "Demolish" or "Demolition" => StatfeedType.Demolish,
        "Hattrick" => StatfeedType.Hattrick,
        "MVPHattrick" or "MvpHattrick" => StatfeedType.MvpHattrick,
        "Savior" => StatfeedType.Savior,
        "BicycleHit" => StatfeedType.BicycleHit,
        "BreakoutDamage" => StatfeedType.Damage,
        "BreakoutDamageLarge" => StatfeedType.UltraDamage,
        "AerialGoal" => StatfeedType.AerialGoal,
        "BackwardsGoal" => StatfeedType.BackwardsGoal,
        "OvertimeGoal" => StatfeedType.OvertimeGoal,
        "MVP" => StatfeedType.Mvp,
        "Win" => StatfeedType.Win,
        _ => StatfeedType.Other,
    };
}
