namespace RocketLeagueStats.WebApi.Tests.Mapping;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Mapping;
using Xunit;

public sealed class PlayerRefMapperTests
{
    [Fact]
    public void Maps_team0_to_blue()
    {
        var src = new PlayerRef("Hellcat", 1, 0);
        var dto = PlayerRefMapper.ToDto(src);
        Assert.Equal("Hellcat", dto.Name);
        Assert.Equal(1, dto.Shortcut);
        Assert.Equal("blue", dto.Team);
    }

    [Fact]
    public void Maps_team1_to_orange()
    {
        var src = new PlayerRef("Stinkmaster", 2, 1);
        var dto = PlayerRefMapper.ToDto(src);
        Assert.Equal("orange", dto.Team);
    }

    [Fact]
    public void Maps_unknown_team_to_string_unknown()
    {
        var src = new PlayerRef("Glitch", 3, 7);
        var dto = PlayerRefMapper.ToDto(src);
        Assert.Equal("unknown", dto.Team);
    }
}
