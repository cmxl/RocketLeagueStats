namespace RocketLeagueStats.Core.Events;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Strongly-typed projection of <see cref="MatchStateSnapshot.RawData"/>. The wire ships full
/// match state on every UpdateState tick; this surface exposes the fields the live UI uses
/// (roster, team metadata, arena) without each consumer re-parsing JsonElements ad-hoc.
/// Use <see cref="TryParse"/> — it returns false for malformed payloads so callers can ignore
/// them instead of throwing.
/// </summary>
public sealed record MatchStateSnapshotData(
    string? MatchGuid,
    IReadOnlyList<SnapshotPlayer> Players,
    IReadOnlyList<SnapshotTeam> Teams,
    string? Arena)
{
    public static bool TryParse(JsonElement raw, out MatchStateSnapshotData? data)
    {
        data = null;
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var matchGuid = raw.TryGetProperty(nameof(MatchGuid), out var guidEl) && guidEl.ValueKind == JsonValueKind.String
            ? guidEl.GetString()
            : null;

        IReadOnlyList<SnapshotPlayer> players = raw.TryGetProperty(nameof(Players), out var playersEl) && playersEl.ValueKind == JsonValueKind.Array
            ? ReadPlayers(playersEl)
            : [];

        var (teams, arena) = raw.TryGetProperty("Game", out var gameEl) && gameEl.ValueKind == JsonValueKind.Object
            ? ReadGame(gameEl)
            : ([], null);

        data = new MatchStateSnapshotData(matchGuid, players, teams, arena);
        return true;
    }

    private static List<SnapshotPlayer> ReadPlayers(JsonElement players)
    {
        var list = new List<SnapshotPlayer>(players.GetArrayLength());
        foreach (var p in players.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = p.TryGetProperty("Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString() ?? string.Empty
                : string.Empty;
            var primaryId = p.TryGetProperty("PrimaryId", out var pidEl) && pidEl.ValueKind == JsonValueKind.String
                ? pidEl.GetString() ?? string.Empty
                : string.Empty;
            var shortcut = p.TryGetProperty("Shortcut", out var scEl) && scEl.ValueKind == JsonValueKind.Number
                ? scEl.GetInt32()
                : 0;
            var teamNum = p.TryGetProperty("TeamNum", out var tnEl) && tnEl.ValueKind == JsonValueKind.Number
                ? tnEl.GetInt32()
                : 0;

            list.Add(new SnapshotPlayer(name, primaryId, ExtractPlatform(primaryId), shortcut, teamNum));
        }

        return list;
    }

    private static (List<SnapshotTeam> Teams, string? Arena) ReadGame(JsonElement game)
    {
        var teams = new List<SnapshotTeam>();
        if (game.TryGetProperty(nameof(Teams), out var teamsEl) && teamsEl.ValueKind == JsonValueKind.Array)
        {
            teams.Capacity = teamsEl.GetArrayLength();
            foreach (var t in teamsEl.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = t.TryGetProperty("Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty;
                var teamNum = t.TryGetProperty("TeamNum", out var tnEl) && tnEl.ValueKind == JsonValueKind.Number
                    ? tnEl.GetInt32()
                    : 0;
                var primary = t.TryGetProperty("ColorPrimary", out var cp) && cp.ValueKind == JsonValueKind.String
                    ? cp.GetString() ?? string.Empty
                    : string.Empty;
                var secondary = t.TryGetProperty("ColorSecondary", out var cs) && cs.ValueKind == JsonValueKind.String
                    ? cs.GetString() ?? string.Empty
                    : string.Empty;

                teams.Add(new SnapshotTeam(name, teamNum, primary, secondary));
            }
        }

        var arena = game.TryGetProperty(nameof(Arena), out var arenaEl) && arenaEl.ValueKind == JsonValueKind.String
            ? arenaEl.GetString()
            : null;

        return (teams, arena);
    }

    /// <summary>
    /// Extracts the platform identifier from a Stats API <c>PrimaryId</c> string. The wire format
    /// is <c>{Platform}|{StableId}|{Variant}</c> — e.g. <c>Steam|76561198050197413|0</c>. Returns
    /// the first segment before the pipe, or empty string if the input is malformed.
    /// </summary>
    public static string ExtractPlatform(string? primaryId)
    {
        if (string.IsNullOrEmpty(primaryId))
        {
            return string.Empty;
        }

        var pipe = primaryId.IndexOf('|', System.StringComparison.Ordinal);
        return pipe <= 0 ? string.Empty : primaryId[..pipe];
    }
}

/// <summary>
/// One entry in <see cref="MatchStateSnapshotData.Players"/>. <see cref="Platform"/> is derived
/// from the leading segment of <see cref="PrimaryId"/> (Steam/Epic/Switch/PS4/XboxOne/...).
/// </summary>
public sealed record SnapshotPlayer(
    string Name,
    string PrimaryId,
    string Platform,
    int Shortcut,
    int TeamNum);

/// <summary>
/// One entry in <see cref="MatchStateSnapshotData.Teams"/>. Color values are 6-digit hex without
/// a leading <c>#</c> (e.g. <c>1873FF</c>).
/// </summary>
public sealed record SnapshotTeam(
    string Name,
    int TeamNum,
    string ColorPrimary,
    string ColorSecondary);
