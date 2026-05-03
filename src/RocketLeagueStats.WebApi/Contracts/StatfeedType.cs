namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>
/// Statfeed event categories. Mapped from RL's wire <c>EventName</c> field
/// (PascalCase identifiers like <c>BreakoutDamageLarge</c>); the human-readable
/// label (e.g. <c>"Ultra Damage"</c>) comes through on <see cref="StatfeedDto.DisplayName"/>.
/// Unrecognized event names fall through to <see cref="Other"/>.
/// </summary>
public enum StatfeedType
{
    Other = 0,
    Save,
    EpicSave,
    Demolish,
    Hattrick,
    MvpHattrick,
    Savior,
    BicycleHit,
    Damage,
    UltraDamage,
    AerialGoal,
    BackwardsGoal,
    OvertimeGoal,
    BicycleGoal,
    LongGoal,
    PoolShot,
    Mvp,
    Win,
}
