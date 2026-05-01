namespace RocketLeagueStats.Core.Events;

using System.Text.Json.Serialization;

public readonly record struct Vec3(
    [property: JsonPropertyName("X")] double X,
    [property: JsonPropertyName("Y")] double Y,
    [property: JsonPropertyName("Z")] double Z);
