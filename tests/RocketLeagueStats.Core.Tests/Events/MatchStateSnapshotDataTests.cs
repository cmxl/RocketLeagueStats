namespace RocketLeagueStats.Core.Tests.Events;

using System.Text.Json;
using RocketLeagueStats.Core.Events;

public sealed class MatchStateSnapshotDataTests
{
    [Fact]
    public void Parses_real_match_snapshot_with_full_roster_and_team_colors()
    {
        // Verbatim shape from artifacts/.../logs/snapshots/snapshot-20260502-195525-match002.json
        // (multi-platform 3v3 — Steam, Epic, Switch on orange; PS4/XboxOne on blue).
        const string json = """
        {
          "MatchGuid": "D22C143A11F146607ABA7DBDE3DA7507",
          "Players": [
            {"Name":"cmxl","PrimaryId":"Steam|76561198050197413|0","Shortcut":5,"TeamNum":1},
            {"Name":"BoogerEater120","PrimaryId":"Epic|1bf36107886043408524b982650e6cf7|0","Shortcut":6,"TeamNum":1},
            {"Name":"Dave","PrimaryId":"Switch|15371672872062723611|0","Shortcut":7,"TeamNum":1},
            {"Name":"B4D_Morais","PrimaryId":"PS4|5649432690749742010|0","Shortcut":1,"TeamNum":0},
            {"Name":"yam-premium804","PrimaryId":"PS4|4457777617304507530|0","Shortcut":2,"TeamNum":0},
            {"Name":"FluegelKobra751","PrimaryId":"XboxOne|2535424472191696|0","Shortcut":3,"TeamNum":0}
          ],
          "Game": {
            "Teams": [
              {"Name":"Blue","TeamNum":0,"Score":0,"ColorPrimary":"1873FF","ColorSecondary":"E5E5E5"},
              {"Name":"Orange","TeamNum":1,"Score":0,"ColorPrimary":"C26418","ColorSecondary":"E5E5E5"}
            ],
            "Arena": "street_p"
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        Assert.True(MatchStateSnapshotData.TryParse(doc.RootElement, out var data));
        Assert.NotNull(data);

        Assert.Equal("D22C143A11F146607ABA7DBDE3DA7507", data!.MatchGuid);
        Assert.Equal(6, data.Players.Count);
        Assert.Equal(2, data.Teams.Count);
        Assert.Equal("street_p", data.Arena);

        var cmxl = data.Players.Single(p => p.Name == "cmxl");
        Assert.Equal("Steam", cmxl.Platform);
        Assert.Equal(5, cmxl.Shortcut);
        Assert.Equal(1, cmxl.TeamNum);

        Assert.Equal("Epic", data.Players.Single(p => p.Name == "BoogerEater120").Platform);
        Assert.Equal("Switch", data.Players.Single(p => p.Name == "Dave").Platform);
        Assert.Equal("PS4", data.Players.Single(p => p.Name == "B4D_Morais").Platform);
        Assert.Equal("XboxOne", data.Players.Single(p => p.Name == "FluegelKobra751").Platform);

        var blue = data.Teams.Single(t => t.TeamNum == 0);
        Assert.Equal("Blue", blue.Name);
        Assert.Equal("1873FF", blue.ColorPrimary);
        Assert.Equal("E5E5E5", blue.ColorSecondary);

        var orange = data.Teams.Single(t => t.TeamNum == 1);
        Assert.Equal("Orange", orange.Name);
        Assert.Equal("C26418", orange.ColorPrimary);
    }

    [Fact]
    public void Parses_freeplay_snapshot_with_empty_MatchGuid_and_grey_team_colors()
    {
        // Verbatim shape from artifacts/.../logs/snapshots/snapshot-20260502-195455-match001.json.
        // Free-play uses MatchGuid="" and the unbranded grey palette.
        const string json = """
        {
          "MatchGuid": "",
          "Players": [
            {"Name":"cmxl","PrimaryId":"Steam|76561198050197413|0","Shortcut":1,"TeamNum":0}
          ],
          "Game": {
            "Teams": [
              {"Name":"Blue","TeamNum":0,"ColorPrimary":"959595","ColorSecondary":"E5E5E5"},
              {"Name":"Orange","TeamNum":1,"ColorPrimary":"959595","ColorSecondary":"E5E5E5"}
            ],
            "Arena": "EuroStadium_Dusk_P"
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        Assert.True(MatchStateSnapshotData.TryParse(doc.RootElement, out var data));
        Assert.NotNull(data);

        Assert.Equal(string.Empty, data!.MatchGuid);
        Assert.Equal("EuroStadium_Dusk_P", data.Arena);
        Assert.Single(data.Players);
        Assert.Equal("Steam", data.Players[0].Platform);
        Assert.All(data.Teams, t => Assert.Equal("959595", t.ColorPrimary));
    }

    [Fact]
    public void Returns_false_for_non_object_input()
    {
        using var doc = JsonDocument.Parse("\"not-an-object\"");
        Assert.False(MatchStateSnapshotData.TryParse(doc.RootElement, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void Tolerates_missing_Players_and_Game_sections()
    {
        using var doc = JsonDocument.Parse("""{"MatchGuid":"abc"}""");
        Assert.True(MatchStateSnapshotData.TryParse(doc.RootElement, out var data));
        Assert.NotNull(data);
        Assert.Equal("abc", data!.MatchGuid);
        Assert.Empty(data.Players);
        Assert.Empty(data.Teams);
        Assert.Null(data.Arena);
    }

    [Theory]
    [InlineData("Steam|76561198050197413|0", "Steam")]
    [InlineData("Epic|1bf36107886043408524b982650e6cf7|0", "Epic")]
    [InlineData("Switch|15371672872062723611|0", "Switch")]
    [InlineData("PS4|5649432690749742010|0", "PS4")]
    [InlineData("XboxOne|2535424472191696|0", "XboxOne")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("noseparator", "")]
    [InlineData("|missingplatform|0", "")]
    public void ExtractPlatform_returns_first_pipe_segment_or_empty(string? primaryId, string expected) =>
        Assert.Equal(expected, MatchStateSnapshotData.ExtractPlatform(primaryId));
}
