namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class MatchTypeClassifierTests
{
    [Theory]
    [InlineData("Ranked1v1", MatchType.Ranked1v1)]
    [InlineData("Ranked2v2", MatchType.Ranked2v2)]
    [InlineData("Ranked3v3", MatchType.Ranked3v3)]
    [InlineData("Casual1v1", MatchType.Casual)]
    [InlineData("Casual2v2", MatchType.Casual)]
    [InlineData("Casual3v3", MatchType.Casual)]
    [InlineData("Tournament", MatchType.Tournament)]
    [InlineData("Private", MatchType.Private)]
    [InlineData("PrivateMatch", MatchType.Private)]
    [InlineData("FreePlay", MatchType.FreePlay)]
    [InlineData("Training", MatchType.Training)]
    [InlineData("CustomTraining", MatchType.Training)]
    public void Classifies_known_playlist_strings(string playlist, MatchType expected) =>
        Assert.Equal(expected, MatchTypeClassifier.FromPlaylist(playlist));

    [Theory]
    [InlineData("")]
    [InlineData("SomethingWeird")]
    [InlineData(null)]
    public void Returns_Unknown_for_unrecognized_or_null_playlists(string? playlist) =>
        Assert.Equal(MatchType.Unknown, MatchTypeClassifier.FromPlaylist(playlist));

    [Fact]
    public void Is_case_insensitive()
    {
        Assert.Equal(MatchType.Ranked3v3, MatchTypeClassifier.FromPlaylist("ranked3v3"));
        Assert.Equal(MatchType.FreePlay, MatchTypeClassifier.FromPlaylist("FREEPLAY"));
    }
}
