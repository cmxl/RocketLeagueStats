using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RocketLeagueStats.Core.GameSetup;

namespace RocketLeagueStats.Core.Tests.GameSetup;

public class StatsApiConfigWriterTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "RLStats-Cfg-" + Guid.NewGuid().ToString("N"));
    private readonly string installRoot;
    private readonly string iniPath;
    private readonly IProcessLookup processLookup = Substitute.For<IProcessLookup>();

    public StatsApiConfigWriterTests()
    {
        this.installRoot = this.tempRoot;
        Directory.CreateDirectory(Path.Combine(this.installRoot, "TAGame", "Config"));
        this.iniPath = Path.Combine(this.installRoot, "TAGame", "Config", "DefaultStatsAPI.ini");
        this.processLookup.IsProcessRunning("RocketLeague").Returns(false);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.tempRoot, recursive: true);
        }
        catch (IOException) { /* temp dir may already be gone */ }
        catch (UnauthorizedAccessException) { /* ignore on cleanup */ }

        GC.SuppressFinalize(this);
    }

    private StatsApiConfigWriter NewWriter() =>
        new(this.processLookup, NullLogger<StatsApiConfigWriter>.Instance);

    private RocketLeagueInstall Install() => new(this.installRoot, RocketLeagueInstallSource.Steam);

    [Fact]
    public void Creates_ini_when_file_does_not_exist()
    {
        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.True(outcome.Changed);
        var lines = File.ReadAllLines(this.iniPath);
        Assert.Contains("PacketSendRate=30", lines);
        Assert.Contains("Port=49123", lines);
    }

    [Fact]
    public void Reports_no_changes_when_ini_already_correct()
    {
        File.WriteAllText(this.iniPath, "[TAGame.MatchStatsExporter_TA]\nPacketSendRate=30\nPort=49123\n");

        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.False(outcome.Changed);
        Assert.Empty(outcome.ChangedKeys);
    }

    [Fact]
    public void Updates_only_misconfigured_keys_and_preserves_unrelated_keys()
    {
        File.WriteAllText(
            this.iniPath,
            "[TAGame.MatchStatsExporter_TA]\n" +
            "PacketSendRate=0\n" +
            "Port=49123\n" +
            "ExtraKey=PreservedValue\n" +
            "[OtherSection]\n" +
            "Foo=Bar\n");

        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.True(outcome.Changed);
        Assert.Contains("PacketSendRate", outcome.ChangedKeys);
        Assert.DoesNotContain("Port", outcome.ChangedKeys);
        var contents = File.ReadAllText(this.iniPath);
        Assert.Contains("PacketSendRate=30", contents, StringComparison.Ordinal);
        Assert.Contains("ExtraKey=PreservedValue", contents, StringComparison.Ordinal);
        Assert.Contains("Foo=Bar", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_write_when_RocketLeague_is_running()
    {
        File.WriteAllText(this.iniPath, "[TAGame.MatchStatsExporter_TA]\nPacketSendRate=0\nPort=49123\n");
        this.processLookup.IsProcessRunning("RocketLeague").Returns(true);

        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.False(outcome.Changed);
        Assert.Contains("running", outcome.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PacketSendRate=0", File.ReadAllText(this.iniPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Creates_dated_backup_before_writing()
    {
        File.WriteAllText(this.iniPath, "[TAGame.MatchStatsExporter_TA]\nPacketSendRate=0\nPort=49123\n");

        this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var backupPath = $"{this.iniPath}.bak.{stamp}";
        Assert.True(File.Exists(backupPath));
        Assert.Contains("PacketSendRate=0", File.ReadAllText(backupPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_create_second_backup_for_same_day()
    {
        File.WriteAllText(this.iniPath, "[TAGame.MatchStatsExporter_TA]\nPacketSendRate=0\nPort=49123\n");
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var backupPath = $"{this.iniPath}.bak.{stamp}";
        File.WriteAllText(backupPath, "preexisting backup contents");

        this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.Equal("preexisting backup contents", File.ReadAllText(backupPath));
    }

    [Fact]
    public void Round_trips_real_world_CRLF_ini_byte_for_byte_when_already_configured()
    {
        // Mirrors the bytes Rocket League ships in DefaultStatsAPI.ini: CRLF endings, comments, no trailing newline.
        const string crlfContent =
            "[TAGame.MatchStatsExporter_TA]\r\n" +
            "\r\n" +
            "; Port the client will listen for connections on\r\n" +
            "Port=49123\r\n" +
            "\r\n" +
            "; How many times per second the game sends the update state (capped at 120, 0 disables this feature)\r\n" +
            "PacketSendRate=30";
        File.WriteAllBytes(this.iniPath, System.Text.Encoding.UTF8.GetBytes(crlfContent));
        var beforeBytes = File.ReadAllBytes(this.iniPath);

        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.False(outcome.Changed);
        var afterBytes = File.ReadAllBytes(this.iniPath);
        Assert.Equal(beforeBytes, afterBytes);
    }

    [Fact]
    public void Updates_in_place_under_existing_section_without_adding_a_new_section()
    {
        // The exact 216-byte ini Rocket League ships, with PacketSendRate=0 (disabled).
        const string crlfContent =
            "[TAGame.MatchStatsExporter_TA]\r\n" +
            "\r\n" +
            "; Port the client will listen for connections on\r\n" +
            "Port=49123\r\n" +
            "\r\n" +
            "; How many times per second the game sends the update state (capped at 120, 0 disables this feature)\r\n" +
            "PacketSendRate=0";
        File.WriteAllBytes(this.iniPath, System.Text.Encoding.UTF8.GetBytes(crlfContent));

        var outcome = this.NewWriter().EnsureConfigured(this.Install(), new StatsApiConfigDesired(30, 49123));

        Assert.True(outcome.Changed);
        Assert.Equal(["PacketSendRate"], outcome.ChangedKeys);

        const string expected =
            "[TAGame.MatchStatsExporter_TA]\r\n" +
            "\r\n" +
            "; Port the client will listen for connections on\r\n" +
            "Port=49123\r\n" +
            "\r\n" +
            "; How many times per second the game sends the update state (capped at 120, 0 disables this feature)\r\n" +
            "PacketSendRate=30";
        Assert.Equal(expected, File.ReadAllText(this.iniPath));
        Assert.DoesNotContain("[StatsAPI]", File.ReadAllText(this.iniPath), StringComparison.Ordinal);
    }
}
