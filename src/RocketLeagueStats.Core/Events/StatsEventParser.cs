namespace RocketLeagueStats.Core.Events;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public sealed class StatsEventParser
{
    /// <summary>
    /// Parses a single envelope from a UTF-8 byte buffer. The hot-path entry point — used by
    /// <see cref="Connection.StatsApiClient"/> with framed bytes from the socket buffer.
    /// </summary>
    /// <remarks>
    /// The Stats API plugin double-encodes the payload: the outer <c>Data</c> field is a JSON string
    /// whose content is itself JSON. When that's the case we re-parse the inner content before
    /// dispatching, so typed events see a structured object regardless of which framing the plugin used.
    /// The plugin does not populate <c>Timestamp</c>, so we stamp arrival time when the envelope omits it.
    /// </remarks>
    public static StatsEvent Parse(ReadOnlyMemory<byte> jsonBytes)
    {
        if (jsonBytes.IsEmpty)
        {
            throw new ArgumentException("JSON payload is empty.", nameof(jsonBytes));
        }

        var envelope = JsonSerializer.Deserialize(jsonBytes.Span, StatsEventJsonContext.Default.StatsEnvelope)
                       ?? throw new JsonException("Envelope deserialized to null.");

        var arrivalTimestamp = envelope.Timestamp ?? DateTimeOffset.UtcNow;

        JsonDocument? innerDoc = null;
        try
        {
            var data = envelope.Data;
            if (data.ValueKind == JsonValueKind.String)
            {
                var inner = data.GetString();
                if (!string.IsNullOrEmpty(inner))
                {
                    innerDoc = JsonDocument.Parse(inner);
                    data = innerDoc.RootElement;
                }
            }

            return Dispatch(envelope, data, arrivalTimestamp);
        }
        finally
        {
            innerDoc?.Dispose();
        }
    }

    /// <summary>
    /// String-friendly overload kept for tests and ad-hoc tooling. Production callers should use the
    /// <see cref="ReadOnlyMemory{T}"/> overload to avoid the UTF-8 round-trip.
    /// </summary>
    public static StatsEvent Parse(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            throw new ArgumentException("Line is empty.", nameof(jsonLine));
        }

        return Parse(Encoding.UTF8.GetBytes(jsonLine).AsMemory());
    }

    private static StatsEvent Dispatch(StatsEnvelope envelope, JsonElement data, DateTimeOffset timestamp) =>
        envelope.Event switch
        {
            // Discrete events with typed Data payloads
            KnownEvents.BallHit => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.BallHitEvent),
            KnownEvents.CrossbarHit => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.CrossbarHitEvent),
            KnownEvents.GoalScored => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.GoalScoredEvent),
            KnownEvents.MatchEnded => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.MatchEndedEvent),
            KnownEvents.StatfeedEvent => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.StatfeedEvent),
            KnownEvents.ClockUpdatedSeconds => DeserializeData(envelope, data, timestamp, StatsEventJsonContext.Default.ClockUpdatedSecondsEvent),

            // MatchGuid-only marker events (no payload to deserialize — extract MatchGuid from Data)
            KnownEvents.MatchCreated => StampMarker(new MatchCreatedEvent(), envelope, data, timestamp),
            KnownEvents.MatchInitialized => StampMarker(new MatchInitializedEvent(), envelope, data, timestamp),
            KnownEvents.MatchDestroyed => StampMarker(new MatchDestroyedEvent(), envelope, data, timestamp),
            KnownEvents.MatchPaused => StampMarker(new MatchPausedEvent(), envelope, data, timestamp),
            KnownEvents.MatchUnpaused => StampMarker(new MatchUnpausedEvent(), envelope, data, timestamp),
            KnownEvents.CountdownBegin => StampMarker(new CountdownBeginEvent(), envelope, data, timestamp),
            KnownEvents.RoundStarted => StampMarker(new RoundStartedEvent(), envelope, data, timestamp),
            KnownEvents.GoalReplayStart => StampMarker(new GoalReplayStartEvent(), envelope, data, timestamp),
            KnownEvents.GoalReplayWillEnd => StampMarker(new GoalReplayWillEndEvent(), envelope, data, timestamp),
            KnownEvents.GoalReplayEnd => StampMarker(new GoalReplayEndEvent(), envelope, data, timestamp),
            KnownEvents.ReplayCreated => StampMarker(new ReplayCreatedEvent(), envelope, data, timestamp),
            KnownEvents.PodiumStart => StampMarker(new PodiumStartEvent(), envelope, data, timestamp),

            // Observed-on-wire-only (undocumented) — see MatchLifecycleEvents.cs comment.
            KnownEvents.ReplayPlaybackStart => StampMarker(new ReplayPlaybackStartEvent(), envelope, data, timestamp),
            KnownEvents.ReplayWillEnd => StampMarker(new ReplayWillEndEvent(), envelope, data, timestamp),
            KnownEvents.ReplayPlaybackEnd => StampMarker(new ReplayPlaybackEndEvent(), envelope, data, timestamp),

            // Periodic state and forward-compat fallback (raw JSON kept for replay/aggregation)
            KnownEvents.UpdateState => new MatchStateSnapshot
            {
                EventName = envelope.Event,
                Timestamp = timestamp,
                MatchGuid = envelope.MatchGuid,
                RawData = data.Clone(),
            },
            _ => new UnknownDiscreteEvent
            {
                EventName = envelope.Event,
                Timestamp = timestamp,
                MatchGuid = envelope.MatchGuid,
                RawData = data.ValueKind == JsonValueKind.Undefined ? default : data.Clone(),
            },
        };

    private static T DeserializeData<T>(StatsEnvelope envelope, JsonElement data, DateTimeOffset timestamp, JsonTypeInfo<T> typeInfo)
        where T : StatsEvent
    {
        var typed = data.Deserialize(typeInfo)
                    ?? throw new JsonException($"Data for event '{envelope.Event}' deserialized to null.");
        return Stamp(typed, envelope, timestamp);
    }

    private static T Stamp<T>(T evt, StatsEnvelope envelope, DateTimeOffset timestamp)
        where T : StatsEvent =>
        evt with
        {
            EventName = envelope.Event,
            Timestamp = timestamp,
            // The wire puts MatchGuid INSIDE Data per the official docs (the envelope shape from the
            // sample only carries Event + Data). For typed events, STJ already pulls it into evt.MatchGuid
            // via the inherited property; for marker events we just-`new`'d, evt.MatchGuid is null and we
            // fall back to envelope.MatchGuid (in case some build of the plugin promotes it).
            MatchGuid = evt.MatchGuid ?? envelope.MatchGuid,
        };

    private static StatsEvent StampMarker<TEvent>(TEvent evt, StatsEnvelope envelope, JsonElement data, DateTimeOffset timestamp)
        where TEvent : StatsEvent
    {
        // Marker records skip DeserializeData, so we have to extract MatchGuid from Data manually.
        var matchGuid = TryReadMatchGuid(data) ?? envelope.MatchGuid;
        return evt with
        {
            EventName = envelope.Event,
            Timestamp = timestamp,
            MatchGuid = matchGuid,
        };
    }

    private static string? TryReadMatchGuid(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("MatchGuid", out var mg)
            && mg.ValueKind == JsonValueKind.String
                ? mg.GetString()
                : null;
}
