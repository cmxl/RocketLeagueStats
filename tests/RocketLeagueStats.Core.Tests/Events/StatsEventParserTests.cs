using System.Text.Json;
using RocketLeagueStats.Core.Events;

namespace RocketLeagueStats.Core.Tests.Events;

public class StatsEventParserTests
{
    [Fact]
    public void Parses_GoalScored_with_typed_data_per_official_docs_schema()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/goal-scored.json");

        var evt = StatsEventParser.Parse(json);

        var goal = Assert.IsType<GoalScoredEvent>(evt);
        Assert.Equal("Karbon", goal.Scorer.Name);
        Assert.Equal(1, goal.Scorer.Shortcut);
        Assert.Equal(0, goal.Scorer.TeamNum);
        Assert.NotNull(goal.Assister);
        Assert.Equal("Daniel", goal.Assister!.Value.Name);
        Assert.Equal(1834.5, goal.GoalSpeed);
        Assert.Equal(127.5, goal.GoalTime);
        Assert.Equal(-2944, goal.ImpactLocation.Y);
        Assert.Equal("abc-123", goal.MatchGuid);
        Assert.Equal(KnownEvents.GoalScored, goal.EventName);
        Assert.NotNull(goal.BallLastTouch);
        Assert.Equal(1500, goal.BallLastTouch!.Value.Speed);
    }

    [Fact]
    public void Parses_BallHit_with_typed_data_per_official_docs_schema()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/ball-hit.json");

        var evt = StatsEventParser.Parse(json);

        var hit = Assert.IsType<BallHitEvent>(evt);
        Assert.Single(hit.Players);
        Assert.Equal("Karbon", hit.Players[0].Name);
        Assert.Equal(0, hit.Ball.PreHitSpeed);
        Assert.Equal(1450.2, hit.Ball.PostHitSpeed);
        Assert.Equal(-512, hit.Ball.Location.X);
    }

    [Fact]
    public void Parses_MatchEnded_with_WinnerTeamNum()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/match-ended.json");

        var evt = StatsEventParser.Parse(json);

        var ended = Assert.IsType<MatchEndedEvent>(evt);
        Assert.Equal(0, ended.WinnerTeamNum);
        Assert.Equal(KnownEvents.MatchEnded, ended.EventName);
    }

    [Fact]
    public void Parses_StatfeedEvent_preserving_inner_EventName_as_StatName()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/statfeed-event.json");

        var evt = StatsEventParser.Parse(json);

        var stat = Assert.IsType<StatfeedEvent>(evt);
        // Outer envelope name (from the wire's "Event" field) is preserved on the base.
        Assert.Equal(KnownEvents.StatfeedEvent, stat.EventName);
        // Inner stat asset name (from "Data.EventName") lands on StatName — this is the renamed property.
        Assert.Equal("Demolish", stat.StatName);
        Assert.Equal("Demolition", stat.Type);
        Assert.Equal("PlayerA", stat.MainTarget.Name);
        Assert.NotNull(stat.SecondaryTarget);
        Assert.Equal("PlayerB", stat.SecondaryTarget!.Value.Name);
    }

    [Fact]
    public void Parses_MatchInitialized_carrying_only_MatchGuid()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/match-initialized.json");

        var evt = StatsEventParser.Parse(json);

        Assert.IsType<MatchInitializedEvent>(evt);
        Assert.Equal(KnownEvents.MatchInitialized, evt.EventName);
        Assert.Equal("abc-123", evt.MatchGuid);
    }

    [Fact]
    public void Parses_UpdateState_as_periodic_snapshot_via_raw_data()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/match-state.json");

        var evt = StatsEventParser.Parse(json);

        Assert.IsType<MatchStateSnapshot>(evt);
        Assert.Equal(KnownEvents.UpdateState, evt.EventName);
    }

    [Fact]
    public void Parses_UpdateState_when_Data_is_an_escaped_JSON_string_real_wire_shape()
    {
        // Mirrors the real plugin wire — Data is a JSON-encoded string, not a nested object.
        const string json = """{"Event":"UpdateState","Data":"{\"MatchGuid\":\"abc\",\"Players\":[{\"Name\":\"cmxl\"}]}"}""";

        var evt = StatsEventParser.Parse(json);

        var snapshot = Assert.IsType<MatchStateSnapshot>(evt);
        Assert.Equal(KnownEvents.UpdateState, snapshot.EventName);
        Assert.Equal(JsonValueKind.Object, snapshot.RawData.ValueKind);
        Assert.Equal(JsonValueKind.Array, snapshot.RawData.GetProperty("Players").ValueKind);
        Assert.Equal("cmxl", snapshot.RawData.GetProperty("Players")[0].GetProperty("Name").GetString());
    }

    [Fact]
    public void UpdateState_extracts_MatchGuid_from_inner_Data_not_outer_envelope()
    {
        // Real wire shape captured from the live plugin: the outer envelope has no MatchGuid; the
        // identifier sits INSIDE Data. Typed events get MatchGuid auto-populated by STJ; markers use
        // TryReadMatchGuid. UpdateState used to read only envelope.MatchGuid and silently produced
        // null MatchGuid on every snapshot — which the SQLite writer then dropped, leaving
        // MatchSnapshots empty in production. This test locks in the inner-Data extraction.
        const string json = """{"Event":"UpdateState","Data":{"MatchGuid":"D22C143A11F146607ABA7DBDE3DA7507","Players":[{"Name":"cmxl"}],"Game":{"Teams":[]}}}""";

        var evt = StatsEventParser.Parse(json);

        var snapshot = Assert.IsType<MatchStateSnapshot>(evt);
        Assert.Equal("D22C143A11F146607ABA7DBDE3DA7507", snapshot.MatchGuid);
    }

    [Fact]
    public void UpdateState_falls_back_to_envelope_MatchGuid_when_inner_Data_lacks_it()
    {
        // Defensive: some captures show MatchGuid on the envelope but not in Data. Keep the
        // fallback path so we don't regress on those wire variants.
        const string json = """{"Event":"UpdateState","MatchGuid":"envelope-guid","Data":{"Players":[]}}""";

        var evt = StatsEventParser.Parse(json);

        var snapshot = Assert.IsType<MatchStateSnapshot>(evt);
        Assert.Equal("envelope-guid", snapshot.MatchGuid);
    }

    [Fact]
    public void UpdateState_with_empty_inner_MatchGuid_preserves_empty_string_for_writer_filter()
    {
        // Training / free-play snapshots arrive with an empty inner MatchGuid (verified in
        // logs/snapshots/snapshot-20260502-195455-match001.json). Preserve the empty string
        // verbatim so the SQLite writer's empty-MatchGuid filter can drop it cleanly.
        const string json = """{"Event":"UpdateState","Data":{"MatchGuid":"","Players":[]}}""";

        var evt = StatsEventParser.Parse(json);

        var snapshot = Assert.IsType<MatchStateSnapshot>(evt);
        Assert.Equal(string.Empty, snapshot.MatchGuid);
    }

    [Fact]
    public void Parses_typed_event_when_Data_is_an_escaped_JSON_string()
    {
        // Mirrors the live wire shape captured in the JSONL trace — the BallHit example from the docs,
        // but with Data double-encoded as a JSON string.
        const string json = """{"Event":"BallHit","MatchGuid":"x","Data":"{\"Players\":[{\"Name\":\"P\",\"Shortcut\":1,\"TeamNum\":0}],\"Ball\":{\"PreHitSpeed\":0,\"PostHitSpeed\":1500,\"Location\":{\"X\":1,\"Y\":2,\"Z\":3}}}"}""";

        var evt = StatsEventParser.Parse(json);

        var hit = Assert.IsType<BallHitEvent>(evt);
        Assert.Equal("P", hit.Players[0].Name);
        Assert.Equal(1500, hit.Ball.PostHitSpeed);
    }

    [Fact]
    public void Parses_CrossbarHit_with_full_typed_data()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/crossbar-hit.json");

        var evt = StatsEventParser.Parse(json);

        var crossbar = Assert.IsType<CrossbarHitEvent>(evt);
        Assert.Equal(870.3, crossbar.BallSpeed);
        Assert.Equal(127.5, crossbar.ImpactForce);
        Assert.Equal(120, crossbar.BallLocation.X);
        Assert.NotNull(crossbar.BallLastTouch);
        Assert.Equal("PlayerA", crossbar.BallLastTouch!.Value.Player.Name);
    }

    [Fact]
    public void Parses_ClockUpdatedSeconds_with_TimeSeconds_and_overtime_flag()
    {
        var json = File.ReadAllText("_Fixtures/StatsApiSamples/clock-updated-seconds.json");

        var evt = StatsEventParser.Parse(json);

        var clock = Assert.IsType<ClockUpdatedSecondsEvent>(evt);
        Assert.Equal(180, clock.TimeSeconds);
        Assert.False(clock.Overtime);
    }

    [Theory]
    [InlineData(KnownEvents.MatchCreated, typeof(MatchCreatedEvent))]
    [InlineData(KnownEvents.MatchInitialized, typeof(MatchInitializedEvent))]
    [InlineData(KnownEvents.MatchDestroyed, typeof(MatchDestroyedEvent))]
    [InlineData(KnownEvents.MatchPaused, typeof(MatchPausedEvent))]
    [InlineData(KnownEvents.MatchUnpaused, typeof(MatchUnpausedEvent))]
    [InlineData(KnownEvents.CountdownBegin, typeof(CountdownBeginEvent))]
    [InlineData(KnownEvents.RoundStarted, typeof(RoundStartedEvent))]
    [InlineData(KnownEvents.GoalReplayStart, typeof(GoalReplayStartEvent))]
    [InlineData(KnownEvents.GoalReplayWillEnd, typeof(GoalReplayWillEndEvent))]
    [InlineData(KnownEvents.GoalReplayEnd, typeof(GoalReplayEndEvent))]
    [InlineData(KnownEvents.ReplayCreated, typeof(ReplayCreatedEvent))]
    [InlineData(KnownEvents.PodiumStart, typeof(PodiumStartEvent))]
    public void Marker_events_dispatch_to_their_typed_records(string eventName, Type expectedType)
    {
        // Lock in: every MatchGuid-only event documented by the API has a typed record and never
        // falls through to UnknownDiscreteEvent.
        var json = "{\"Event\":\"" + eventName + "\",\"Data\":{\"MatchGuid\":\"abc-123\"}}";

        var evt = StatsEventParser.Parse(json);

        Assert.IsType(expectedType, evt);
        Assert.Equal(eventName, evt.EventName);
        Assert.Equal("abc-123", evt.MatchGuid);
    }

    [Fact]
    public void Every_documented_event_dispatches_to_a_typed_record_not_unknown()
    {
        // Coverage gate: if Psyonix added an event to KnownEvents but we forgot a Dispatch arm,
        // this test fails. Mirrors the 19 events listed at https://www.rocketleague.com/en/developer/stats-api.
        var documentedEvents = new[]
        {
            KnownEvents.BallHit,
            KnownEvents.CrossbarHit,
            KnownEvents.GoalScored,
            KnownEvents.StatfeedEvent,
            KnownEvents.MatchCreated,
            KnownEvents.MatchInitialized,
            KnownEvents.MatchDestroyed,
            KnownEvents.MatchEnded,
            KnownEvents.MatchPaused,
            KnownEvents.MatchUnpaused,
            KnownEvents.CountdownBegin,
            KnownEvents.RoundStarted,
            KnownEvents.ClockUpdatedSeconds,
            KnownEvents.GoalReplayStart,
            KnownEvents.GoalReplayWillEnd,
            KnownEvents.GoalReplayEnd,
            KnownEvents.ReplayCreated,
            KnownEvents.PodiumStart,
            KnownEvents.UpdateState,
        };
        Assert.Equal(19, documentedEvents.Length);

        // Use minimal-but-valid Data so DeserializeData paths don't choke on missing required fields.
        var minimalData = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [KnownEvents.BallHit] = """{"Players":[{"Name":"P","Shortcut":1,"TeamNum":0}],"Ball":{"PreHitSpeed":0,"PostHitSpeed":1,"Location":{"X":0,"Y":0,"Z":0}}}""",
            [KnownEvents.CrossbarHit] = """{"BallLocation":{"X":0,"Y":0,"Z":0},"BallSpeed":0,"ImpactForce":0}""",
            [KnownEvents.GoalScored] = """{"GoalSpeed":0,"GoalTime":0,"ImpactLocation":{"X":0,"Y":0,"Z":0},"Scorer":{"Name":"P","Shortcut":1,"TeamNum":0}}""",
            [KnownEvents.StatfeedEvent] = """{"EventName":"Demolish","Type":"Demolition","MainTarget":{"Name":"P","Shortcut":1,"TeamNum":0}}""",
            [KnownEvents.MatchEnded] = """{"WinnerTeamNum":0}""",
            [KnownEvents.ClockUpdatedSeconds] = """{"TimeSeconds":1,"bOvertime":false}""",
            [KnownEvents.UpdateState] = """{"MatchGuid":""}""",
        };

        foreach (var name in documentedEvents)
        {
            var data = minimalData.TryGetValue(name, out var d) ? d : """{"MatchGuid":""}""";
            var json = "{\"Event\":\"" + name + "\",\"Data\":" + data + "}";

            var evt = StatsEventParser.Parse(json);

            Assert.False(evt is UnknownDiscreteEvent, $"Event '{name}' fell through to UnknownDiscreteEvent — missing Dispatch arm in StatsEventParser.");
            Assert.Equal(name, evt.EventName);
        }
    }

    [Fact]
    public void Stamps_Timestamp_as_UtcNow_when_envelope_omits_it()
    {
        // The live plugin doesn't populate Timestamp, so the parser fills it with arrival time.
        const string json = """{"Event":"MatchInitialized","Data":{}}""";
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var evt = StatsEventParser.Parse(json);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.NotNull(evt.Timestamp);
        Assert.InRange(evt.Timestamp!.Value, before, after);
    }
}
