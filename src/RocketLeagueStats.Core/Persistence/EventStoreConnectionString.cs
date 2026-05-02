namespace RocketLeagueStats.Core.Persistence;

/// <summary>
/// Typed wrapper so DI can inject the resolved connection string without colliding with other
/// string registrations.
/// </summary>
public sealed record EventStoreConnectionString(string Value);
