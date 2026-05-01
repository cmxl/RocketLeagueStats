namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>Statfeed event categories (saves, demos, epic saves, hattricks, etc.).</summary>
public enum StatfeedType
{
    Other = 0,
    Save,
    EpicSave,
    Demolish,
    Hattrick,
    MvpHattrick,
}
