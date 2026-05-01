namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>
/// Game-mode classification.
/// Specific types (Ranked1v1, Casual, FreePlay, etc.) are derived from the playlist field
/// in MatchStateSnapshot.RawData when available. Online / Offline are coarse fallbacks set
/// from MatchInitializedEvent.MatchGuid presence (non-empty = Online; empty = Offline) when
/// the snapshot parser hasn't refined the type yet.
/// </summary>
public enum MatchType
{
    Unknown = 0,
    Ranked1v1,
    Ranked2v2,
    Ranked3v3,
    Casual,
    Tournament,
    Private,
    FreePlay,
    Training,

    /// <summary>Coarse: ranked / casual / tournament — refined later when snapshot parsing lands.</summary>
    Online,

    /// <summary>Coarse: training / freeplay / private — refined later when snapshot parsing lands.</summary>
    Offline,
}
