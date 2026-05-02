namespace RocketLeagueStats.WebApi.Contracts;

/// <summary>
/// Team metadata extracted from a <c>MatchStateSnapshot.RawData.Game.Teams[]</c> entry. The
/// colors are 6-digit hex without a leading <c>#</c> (e.g. <c>1873FF</c>) so the frontend can
/// inject them into CSS variables directly. <see cref="Name"/> is the in-game label (typically
/// "Blue" or "Orange"); branded competitive matches may use sponsor names later.
/// </summary>
public sealed record TeamDto(string Name, string ColorPrimary, string ColorSecondary);
