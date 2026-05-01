using Microsoft.Extensions.Logging.Abstractions;
using RocketLeagueStats.Core.GameSetup;

namespace RocketLeagueStats.Core.Tests.GameSetup;

public class GameInstallLocatorTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "RLStats-Tests-" + Guid.NewGuid().ToString("N"));

    public GameInstallLocatorTests() => Directory.CreateDirectory(this.tempRoot);

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

    [Fact]
    public void Locate_returns_Steam_install_when_libraryfolders_lists_appid_252950()
    {
        var steamRoot = Path.Combine(this.tempRoot, "Steam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps", "common", "rocketleague"));

        var vdf = $$"""
            "libraryfolders"
            {
                "0"
                {
                    "path"      "{{steamRoot.Replace("\\", "\\\\")}}"
                    "apps"
                    {
                        "252950"   "1500000000"
                    }
                }
            }
            """;
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), vdf);

        var locator = new GameInstallLocator(
            new GameInstallLocator.Probes
            {
                SteamRoots = [steamRoot],
                EpicManifestRoots = []
            },
            NullLogger<GameInstallLocator>.Instance);

        var result = locator.Locate();

        Assert.NotNull(result);
        Assert.Equal(RocketLeagueInstallSource.Steam, result!.Source);
        Assert.Equal(Path.Combine(steamRoot, "steamapps", "common", "rocketleague"), result.Path);
    }

    [Fact]
    public void Locate_returns_Epic_install_when_manifest_is_RocketLeague()
    {
        var rlInstallDir = Path.Combine(this.tempRoot, "EpicGames", "rocketleague");
        Directory.CreateDirectory(rlInstallDir);
        var manifestDir = Path.Combine(this.tempRoot, "Manifests");
        Directory.CreateDirectory(manifestDir);

        var manifest = $$"""
        {
            "DisplayName": "Rocket League",
            "InstallLocation": "{{rlInstallDir.Replace("\\", "\\\\")}}",
            "CatalogNamespace": "9773aa1aa54f4f7b80e44bef04986cea"
        }
        """;
        File.WriteAllText(Path.Combine(manifestDir, "RocketLeague.item"), manifest);

        var locator = new GameInstallLocator(
            new GameInstallLocator.Probes
            {
                SteamRoots = [],
                EpicManifestRoots = [manifestDir]
            },
            NullLogger<GameInstallLocator>.Instance);

        var result = locator.Locate();

        Assert.NotNull(result);
        Assert.Equal(RocketLeagueInstallSource.Epic, result!.Source);
        Assert.Equal(rlInstallDir, result.Path);
    }

    [Fact]
    public void Locate_returns_null_when_no_install_found()
    {
        var locator = new GameInstallLocator(
            new GameInstallLocator.Probes
            {
                SteamRoots = [Path.Combine(this.tempRoot, "missing-steam")],
                EpicManifestRoots = [Path.Combine(this.tempRoot, "missing-epic")]
            },
            NullLogger<GameInstallLocator>.Instance);

        var result = locator.Locate();

        Assert.Null(result);
    }
}
