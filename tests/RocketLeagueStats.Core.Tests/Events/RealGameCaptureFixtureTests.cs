using System.Text.Json;
using System.Text.Json.Serialization;
using RocketLeagueStats.Core.Events;

namespace RocketLeagueStats.Core.Tests.Events;

/// <summary>
/// Replays the persisted JSONL format produced by <c>JsonlEventLoggerService</c> against a real
/// captured game session (~10 minutes, 957 events spanning a freeplay warmup with empty MatchGuid
/// and a multiplayer overtime match with a real GUID).
///
/// This is a regression corpus for the on-disk format: it locks in the round-trip contract so any
/// future change to a typed event record (renamed property, added <c>[JsonIgnore]</c>, dropped
/// JsonPropertyName, etc.) trips a failing line during replay. It exercises 15 of the 22 events
/// the parser knows about; the four documented events not present in this capture
/// (MatchEnded, PodiumStart, ReplayCreated, UpdateState) and the three doc-only-name replay events
/// (GoalReplay{Start,WillEnd,End}) are still covered by <see cref="StatsEventParserTests"/>.
/// </summary>
public class RealGameCaptureFixtureTests
{
    private const string FixturePath = "_Fixtures/RealGameCapture/rl-stats-real_game.jsonl";

    // Mirrors the deserialization side of JsonlEventLoggerService.SerializerOptions.
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Every_line_deserializes_into_its_typed_event_record()
    {
        var lines = ReadFixtureLines();
        Assert.Equal(957, lines.Length);

        var failures = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            try
            {
                var evt = DeserializeLine(lines[i]);
                if (evt is null)
                {
                    failures.Add($"line {i + 1}: deserialized to null");
                }
                else if (string.IsNullOrEmpty(evt.EventName))
                {
                    failures.Add($"line {i + 1}: empty EventName after round-trip");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"line {i + 1}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Event_name_distribution_matches_captured_session()
    {
        var counts = LoadEvents()
            .GroupBy(e => e.EventName)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(471, counts[KnownEvents.ClockUpdatedSeconds]);
        Assert.Equal(331, counts[KnownEvents.BallHit]);
        Assert.Equal(56, counts[KnownEvents.CrossbarHit]);
        Assert.Equal(35, counts[KnownEvents.StatfeedEvent]);
        Assert.Equal(15, counts[KnownEvents.GoalScored]);
        Assert.Equal(12, counts[KnownEvents.CountdownBegin]);
        Assert.Equal(12, counts[KnownEvents.RoundStarted]);
        Assert.Equal(5, counts[KnownEvents.ReplayWillEnd]);
        Assert.Equal(4, counts[KnownEvents.ReplayPlaybackStart]);
        Assert.Equal(4, counts[KnownEvents.ReplayPlaybackEnd]);
        Assert.Equal(3, counts[KnownEvents.MatchPaused]);
        Assert.Equal(3, counts[KnownEvents.MatchUnpaused]);
        Assert.Equal(2, counts[KnownEvents.MatchCreated]);
        Assert.Equal(2, counts[KnownEvents.MatchInitialized]);
        Assert.Equal(2, counts[KnownEvents.MatchDestroyed]);

        // Exactly 15 distinct event names — capture lacks MatchEnded, PodiumStart, ReplayCreated,
        // UpdateState, and the doc-only-name GoalReplay{Start,WillEnd,End}.
        Assert.Equal(15, counts.Count);
        Assert.DoesNotContain(KnownEvents.MatchEnded, counts.Keys);
        Assert.DoesNotContain(KnownEvents.PodiumStart, counts.Keys);
        Assert.DoesNotContain(KnownEvents.ReplayCreated, counts.Keys);
        Assert.DoesNotContain(KnownEvents.UpdateState, counts.Keys);
    }

    [Fact]
    public void Statfeed_subtypes_cover_basic_and_advanced_celebrations()
    {
        var statfeed = LoadEvents().OfType<StatfeedEvent>().ToArray();
        Assert.Equal(35, statfeed.Length);

        var counts = statfeed
            .GroupBy(s => s.StatName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(12, counts["Shot"]);
        Assert.Equal(8, counts["Demolish"]);
        Assert.Equal(5, counts["Goal"]);
        Assert.Equal(3, counts["Assist"]);
        Assert.Equal(2, counts["EpicSave"]);
        Assert.Equal(2, counts["Save"]);
        Assert.Equal(1, counts["AerialGoal"]);
        Assert.Equal(1, counts["FlipReset"]);
        Assert.Equal(1, counts["OvertimeGoal"]);

        // Only Demolish carries SecondaryTarget (the attacker) in this capture; every other
        // subtype is single-target. Locks in the optional-field semantics of StatfeedEvent.
        Assert.All(
            statfeed.Where(s => string.Equals(s.StatName, "Demolish", StringComparison.Ordinal)),
            s => Assert.NotNull(s.SecondaryTarget));
        Assert.All(
            statfeed.Where(s => !string.Equals(s.StatName, "Demolish", StringComparison.Ordinal)),
            s => Assert.Null(s.SecondaryTarget));
    }

    [Fact]
    public void Clock_ticks_cover_overtime_and_full_round_range()
    {
        var clocks = LoadEvents().OfType<ClockUpdatedSecondsEvent>().ToArray();
        Assert.Equal(471, clocks.Length);

        // The real match went to overtime — 43 ticks were emitted with bOvertime=true.
        Assert.Equal(43, clocks.Count(c => c.Overtime));

        var values = clocks.Select(c => c.TimeSeconds).ToHashSet();
        Assert.Contains(0, values);
        Assert.Contains(300, values);   // 5-minute round timer maximum
    }

    [Fact]
    public void MatchGuid_partition_separates_freeplay_warmup_from_real_match()
    {
        var byGuid = LoadEvents()
            .GroupBy(e => e.MatchGuid ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(2, byGuid.Count);
        Assert.Equal(286, byGuid[string.Empty]);
        Assert.Equal(671, byGuid["3150F0DA11F14534CCE04AAEB78C084D"]);

        // StatfeedEvents only fire after a real match exists — never in freeplay.
        var statfeedGuids = LoadEvents()
            .OfType<StatfeedEvent>()
            .Select(s => s.MatchGuid)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Single(statfeedGuids);
        Assert.Equal("3150F0DA11F14534CCE04AAEB78C084D", statfeedGuids[0]);
    }

    [Fact]
    public void GoalScored_BallLastTouch_is_populated_in_real_match_goals()
    {
        var goals = LoadEvents().OfType<GoalScoredEvent>().ToArray();
        Assert.Equal(15, goals.Length);

        // Every captured goal carried a non-null BallLastTouch — locks in that the field is
        // present in practice even though the type allows null.
        Assert.All(goals, g => Assert.NotNull(g.BallLastTouch));
    }

    private static string[] ReadFixtureLines() =>
        [.. File.ReadAllLines(FixturePath).Where(static l => !string.IsNullOrWhiteSpace(l))];

    private static StatsEvent[] LoadEvents() =>
        [.. ReadFixtureLines().Select(DeserializeLine).OfType<StatsEvent>()];

    /// <summary>
    /// Reads the top-level <c>"Event"</c> field, then deserializes the whole flat line into the
    /// matching record. This intentionally bypasses <see cref="StatsEventParser"/> because the
    /// parser consumes the wire envelope shape (<c>{Event, Data:{...}}</c>) while the on-disk
    /// JSONL format is the typed record flattened by <c>JsonlEventLoggerService</c>.
    /// </summary>
    private static StatsEvent? DeserializeLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var eventName = doc.RootElement.GetProperty("Event").GetString();

        return eventName switch
        {
            KnownEvents.BallHit => JsonSerializer.Deserialize<BallHitEvent>(line, Options),
            KnownEvents.CrossbarHit => JsonSerializer.Deserialize<CrossbarHitEvent>(line, Options),
            KnownEvents.GoalScored => JsonSerializer.Deserialize<GoalScoredEvent>(line, Options),
            KnownEvents.MatchEnded => JsonSerializer.Deserialize<MatchEndedEvent>(line, Options),
            KnownEvents.StatfeedEvent => JsonSerializer.Deserialize<StatfeedEvent>(line, Options),
            KnownEvents.ClockUpdatedSeconds => JsonSerializer.Deserialize<ClockUpdatedSecondsEvent>(line, Options),

            KnownEvents.MatchCreated => JsonSerializer.Deserialize<MatchCreatedEvent>(line, Options),
            KnownEvents.MatchInitialized => JsonSerializer.Deserialize<MatchInitializedEvent>(line, Options),
            KnownEvents.MatchDestroyed => JsonSerializer.Deserialize<MatchDestroyedEvent>(line, Options),
            KnownEvents.MatchPaused => JsonSerializer.Deserialize<MatchPausedEvent>(line, Options),
            KnownEvents.MatchUnpaused => JsonSerializer.Deserialize<MatchUnpausedEvent>(line, Options),
            KnownEvents.CountdownBegin => JsonSerializer.Deserialize<CountdownBeginEvent>(line, Options),
            KnownEvents.RoundStarted => JsonSerializer.Deserialize<RoundStartedEvent>(line, Options),
            KnownEvents.GoalReplayStart => JsonSerializer.Deserialize<GoalReplayStartEvent>(line, Options),
            KnownEvents.GoalReplayWillEnd => JsonSerializer.Deserialize<GoalReplayWillEndEvent>(line, Options),
            KnownEvents.GoalReplayEnd => JsonSerializer.Deserialize<GoalReplayEndEvent>(line, Options),
            KnownEvents.ReplayCreated => JsonSerializer.Deserialize<ReplayCreatedEvent>(line, Options),
            KnownEvents.PodiumStart => JsonSerializer.Deserialize<PodiumStartEvent>(line, Options),
            KnownEvents.ReplayPlaybackStart => JsonSerializer.Deserialize<ReplayPlaybackStartEvent>(line, Options),
            KnownEvents.ReplayWillEnd => JsonSerializer.Deserialize<ReplayWillEndEvent>(line, Options),
            KnownEvents.ReplayPlaybackEnd => JsonSerializer.Deserialize<ReplayPlaybackEndEvent>(line, Options),

            KnownEvents.UpdateState => null,   // wrapped via MatchStateSnapshot.RawData; not asserted here

            _ => throw new InvalidOperationException($"Unrecognized Event name: {eventName}"),
        };
    }
}
