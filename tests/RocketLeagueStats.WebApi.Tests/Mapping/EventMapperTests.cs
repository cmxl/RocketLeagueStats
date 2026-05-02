namespace RocketLeagueStats.WebApi.Tests.Mapping;

using RocketLeagueStats.Core.Events;
using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Mapping;
using Xunit;

public sealed class EventMapperTests
{
    private static StatfeedEvent Statfeed(string eventName, string displayType) => new()
    {
        StatName = eventName,
        Type = displayType,
        MainTarget = new PlayerRef("P1", 1, 0),
        SecondaryTarget = null,
    };

    [Theory]
    [InlineData("Save", "Save", StatfeedType.Save)]
    [InlineData("EpicSave", "Epic Save", StatfeedType.EpicSave)]
    [InlineData("Demolish", "Demolition", StatfeedType.Demolish)]
    [InlineData("Demolition", "Demolition", StatfeedType.Demolish)]
    [InlineData("Hattrick", "Hattrick", StatfeedType.Hattrick)]
    [InlineData("MVPHattrick", "MVP Hattrick", StatfeedType.MvpHattrick)]
    [InlineData("MvpHattrick", "MVP Hattrick", StatfeedType.MvpHattrick)]
    [InlineData("Savior", "Savior", StatfeedType.Savior)]
    [InlineData("BicycleHit", "Bicycle Hit", StatfeedType.BicycleHit)]
    [InlineData("BreakoutDamage", "Damage", StatfeedType.Damage)]
    [InlineData("BreakoutDamageLarge", "Ultra Damage", StatfeedType.UltraDamage)]
    [InlineData("AerialGoal", "Aerial Goal", StatfeedType.AerialGoal)]
    [InlineData("BackwardsGoal", "Backwards Goal", StatfeedType.BackwardsGoal)]
    [InlineData("OvertimeGoal", "Overtime Goal", StatfeedType.OvertimeGoal)]
    [InlineData("MVP", "MVP", StatfeedType.Mvp)]
    [InlineData("Win", "Win", StatfeedType.Win)]
    public void Classifies_known_event_names(string eventName, string displayType, StatfeedType expected)
    {
        var dto = EventMapper.ToDto(Statfeed(eventName, displayType), matchClockSeconds: 60);
        Assert.Equal(expected, dto.Type);
        Assert.Equal(displayType, dto.DisplayName);
    }

    [Fact]
    public void Unknown_event_name_falls_through_to_Other_with_display_passthrough()
    {
        var dto = EventMapper.ToDto(Statfeed("FutureCelebration", "Future Celebration"), matchClockSeconds: 0);
        Assert.Equal(StatfeedType.Other, dto.Type);
        Assert.Equal("Future Celebration", dto.DisplayName);
    }

    [Fact]
    public void Falls_back_to_event_name_when_display_type_missing()
    {
        // RL has been observed to occasionally emit a statfeed with empty Type — we keep the
        // wire EventName as a passable display label rather than rendering empty string.
        var dto = EventMapper.ToDto(Statfeed("WeirdNewThing", string.Empty), matchClockSeconds: 0);
        Assert.Equal("WeirdNewThing", dto.DisplayName);
    }
}
