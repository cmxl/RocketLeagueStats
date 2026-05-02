using global::Mediator;

// Scoped (not Singleton) so handlers can directly consume scoped DI services like the
// StatsDbContext used by MatchHistoryReader. Handlers that previously held no per-request
// state still cost essentially nothing as scoped — they're stateless wrappers.
[assembly: MediatorOptions(Namespace = "RocketLeagueStats.WebApi.Mediator.Generated", ServiceLifetime = ServiceLifetime.Scoped)]
