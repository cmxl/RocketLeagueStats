namespace RocketLeagueStats.WebApi.Tests.Services;

using RocketLeagueStats.WebApi.Contracts;
using RocketLeagueStats.WebApi.Services;
using Xunit;

public sealed class SettingsStoreTests : IDisposable
{
    private static readonly string[] OneFriend = ["Stinkmaster"];

    private readonly string tempDir;

    public SettingsStoreTests()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), $"rls-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Returns_defaults_when_file_missing()
    {
        var store = new SettingsStore(this.tempDir);
        var settings = await store.GetAsync(CancellationToken.None);
        Assert.Null(settings.PlayerName);
        Assert.Empty(settings.FriendNames);
        Assert.False(settings.ShowTrainingInHistory);
    }

    [Fact]
    public async Task Round_trips_settings_via_save_then_get()
    {
        var store = new SettingsStore(this.tempDir);
        var dto = new SettingsDto("Hellcat", OneFriend, ShowTrainingInHistory: true);
        await store.SaveAsync(dto, CancellationToken.None);

        var loaded = await store.GetAsync(CancellationToken.None);
        Assert.Equal("Hellcat", loaded.PlayerName);
        Assert.Equal(OneFriend, loaded.FriendNames);
        Assert.True(loaded.ShowTrainingInHistory);
    }

    [Fact]
    public async Task Returns_defaults_and_backs_up_corrupted_file()
    {
        var path = Path.Combine(this.tempDir, "settings.json");
        await File.WriteAllTextAsync(path, "this is not valid json {{{");

        var store = new SettingsStore(this.tempDir);
        var settings = await store.GetAsync(CancellationToken.None);
        Assert.Null(settings.PlayerName);

        var backups = Directory.GetFiles(this.tempDir, "settings.json.bad-*");
        Assert.Single(backups);
    }

    [Fact]
    public async Task Save_creates_directory_if_missing()
    {
        var nested = Path.Combine(this.tempDir, "nested", "dir");
        var store = new SettingsStore(nested);
        await store.SaveAsync(new SettingsDto("Test", [], false), CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(nested, "settings.json")));
    }
}
