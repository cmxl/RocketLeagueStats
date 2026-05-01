using System.Text.Json;
using RocketLeagueStats.Core.Events;

namespace RocketLeagueStats.Core.Tests.Events;

public class ForwardCompatibilityTests
{
    [Fact]
    public void Unknown_event_name_yields_UnknownDiscreteEvent()
    {
        const string json = """{"Event":"FutureEvent","MatchGuid":"x","Data":{"Foo":"bar"}}""";

        var evt = StatsEventParser.Parse(json);

        var unknown = Assert.IsType<UnknownDiscreteEvent>(evt);
        Assert.Equal("FutureEvent", unknown.EventName);
        Assert.Equal("bar", unknown.RawData.GetProperty("Foo").GetString());
    }

    [Fact]
    public void Known_event_with_extra_fields_still_deserializes()
    {
        const string json = """
        {"Event":"GoalScored","MatchGuid":"x","Data":{"GoalSpeed":100,"GoalTime":1.0,"ImpactLocation":{"X":0,"Y":0,"Z":0},"Scorer":{"Name":"P","Shortcut":1,"TeamNum":0},"NewFutureField":"ignored"}}
        """;

        var evt = StatsEventParser.Parse(json);

        var goal = Assert.IsType<GoalScoredEvent>(evt);
        Assert.Equal("P", goal.Scorer.Name);
    }

    [Fact]
    public void Malformed_json_throws_JsonException()
    {
        const string json = "{not json";

        Assert.Throws<JsonException>(() => StatsEventParser.Parse(json));
    }

    [Fact]
    public void Empty_line_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StatsEventParser.Parse(""));
        Assert.Throws<ArgumentException>(() => StatsEventParser.Parse("   "));
    }
}
