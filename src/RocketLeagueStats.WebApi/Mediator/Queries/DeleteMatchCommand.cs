namespace RocketLeagueStats.WebApi.Mediator.Queries;

using global::Mediator;

/// <summary>
/// Removes a single match plus every related row (Events, MatchSnapshots, EventParticipants,
/// PlayerMatchStats) via SQLite's cascade-delete FKs. Returns true if the match existed and was
/// deleted; false if the MatchGuid wasn't found in the database.
/// </summary>
public sealed record DeleteMatchCommand(string MatchId) : ICommand<bool>;
