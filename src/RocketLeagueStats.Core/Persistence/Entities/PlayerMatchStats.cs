namespace RocketLeagueStats.Core.Persistence.Entities;

/// <summary>
/// Per-match per-player wire-authoritative stats. Populated at MatchEnded time from the most
/// recent <c>MatchStateSnapshot</c> seen for the match, so historical recaps can show the same
/// numbers Rocket League itself reported on the in-game scoreboard. Kept separate from
/// <see cref="EventParticipant"/> (which records each individual goal/statfeed participation
/// row) — this table is the per-match summary, that one is the per-event audit log.
/// </summary>
public sealed class PlayerMatchStats
{
    public required string MatchGuid { get; init; }

    public required int Shortcut { get; init; }

    public required string PlayerName { get; init; }

    public required int TeamNum { get; init; }

    public required string Platform { get; init; }

    public required int Score { get; init; }

    public required int Goals { get; init; }

    public required int Assists { get; init; }

    public required int Saves { get; init; }

    public required int Shots { get; init; }

    public required int Touches { get; init; }
}
