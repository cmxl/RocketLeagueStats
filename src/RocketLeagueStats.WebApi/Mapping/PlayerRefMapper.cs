namespace RocketLeagueStats.WebApi.Mapping;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;

internal static class PlayerRefMapper
{
    public static PlayerRefDto ToDto(PlayerRef src) => new(
        Name: src.Name,
        Shortcut: src.Shortcut,
        Team: src.TeamNum switch
        {
            0 => "blue",
            1 => "orange",
            _ => "unknown",
        });
}
