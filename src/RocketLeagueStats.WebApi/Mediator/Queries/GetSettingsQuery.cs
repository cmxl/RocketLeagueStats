namespace RocketLeagueStats.WebApi.Mediator.Queries;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;

public sealed record GetSettingsQuery : IQuery<SettingsDto>;
