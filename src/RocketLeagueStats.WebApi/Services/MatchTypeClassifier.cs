namespace RocketLeagueStats.WebApi.Services;

using RocketLeagueStats.WebApi.Contracts;

internal static class MatchTypeClassifier
{
    public static MatchType FromPlaylist(string? playlist)
    {
        if (string.IsNullOrWhiteSpace(playlist))
        {
            return MatchType.Unknown;
        }

        var p = playlist.Trim();

        if (p.Equals("Ranked1v1", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Ranked1v1;
        }

        if (p.Equals("Ranked2v2", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Ranked2v2;
        }

        if (p.Equals("Ranked3v3", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Ranked3v3;
        }

        if (p.StartsWith("Casual", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Casual;
        }

        if (p.Equals("Tournament", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Tournament;
        }

        if (p.Contains("Private", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Private;
        }

        if (p.Equals("FreePlay", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.FreePlay;
        }

        if (p.Contains("Training", StringComparison.OrdinalIgnoreCase))
        {
            return MatchType.Training;
        }

        return MatchType.Unknown;
    }
}
