namespace RocketLeagueStats.WebApi.Mediator.Queries;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;

public sealed record GetLiveStateQuery : IQuery<LiveStateDto>;
