namespace RocketLeagueStats.Core.Events;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(StatsEnvelope))]
[JsonSerializable(typeof(BallHitEvent))]
[JsonSerializable(typeof(CrossbarHitEvent))]
[JsonSerializable(typeof(GoalScoredEvent))]
[JsonSerializable(typeof(MatchEndedEvent))]
[JsonSerializable(typeof(StatfeedEvent))]
[JsonSerializable(typeof(ClockUpdatedSecondsEvent))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
internal sealed partial class StatsEventJsonContext : JsonSerializerContext;
