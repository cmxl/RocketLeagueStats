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
        MainTarget: PlayerRefMapper.ToDto(evt.MainTarget),
        SecondaryTarget: evt.SecondaryTarget is { } s ? PlayerRefMapper.ToDto(s) : null);

    private static StatfeedType ClassifyStatName(string statName) => statName switch
    {
        "Demolish" or "Demolition" => StatfeedType.Demolish,
        "Save" => StatfeedType.Save,
        "EpicSave" => StatfeedType.EpicSave,
        "Hattrick" => StatfeedType.Hattrick,
        "MVPHattrick" or "MvpHattrick" => StatfeedType.MvpHattrick,
        _ => StatfeedType.Other,
    };
}
