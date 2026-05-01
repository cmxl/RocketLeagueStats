namespace RocketLeagueStats.WebApi.Mediator.Queries;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;

public sealed record UpdateSettingsCommand(SettingsDto Settings) : ICommand<SettingsDto>;
