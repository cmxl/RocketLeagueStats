namespace RocketLeagueStats.WebApi.Mediator.Queries;

using global::Mediator;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services;

public sealed record GetMatchHistoryQuery(
    bool IncludeTraining,
    bool IncludeFreePlay,
    DateTime? From,
    DateTime? To,
    HistorySort Sort) : IQuery<MatchSummaryDto[]>;
