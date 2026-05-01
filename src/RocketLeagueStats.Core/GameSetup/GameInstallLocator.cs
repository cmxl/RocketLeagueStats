namespace RocketLeagueStats.Core.GameSetup;

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

public sealed partial class GameInstallLocator(GameInstallLocator.Probes probes, ILogger<GameInstallLocator> logger) : IGameInstallLocator
{
    private const string RocketLeagueSteamAppId = "252950";

    private static readonly Action<ILogger, string, Exception?> LogSteamVdfMissing =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(GameInstallLocator)), "Steam libraryfolders.vdf missing at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogSteamFound =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(GameInstallLocator)), "Found Rocket League (Steam) at {Path}");

    private static readonly Action<ILogger, string, string, string, Exception?> LogSteamInstallDirMissing =
        LoggerMessage.Define<string, string, string>(LogLevel.Debug, new EventId(3, nameof(GameInstallLocator)), "Steam library {Library} lists app {AppId} but install dir {Install} is missing.");

    private static readonly Action<ILogger, string, Exception?> LogEpicManifestRootMissing =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, nameof(GameInstallLocator)), "Epic manifest root missing at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogEpicFound =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, nameof(GameInstallLocator)), "Found Rocket League (Epic) at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogEpicManifestUnparseable =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, nameof(GameInstallLocator)), "Skipping unparseable Epic manifest at {File}");

    [GeneratedRegex(@"""\d+""\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}", RegexOptions.Singleline)]
    private static partial Regex LibraryEntryPattern();

    [GeneratedRegex(@"""path""\s+""(?<path>[^""]+)""")]
    private static partial Regex LibraryPathPattern();

    [GeneratedRegex(@"""apps""\s*\{(?<apps>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex AppsBlockPattern();

    public RocketLeagueInstall? Locate()
    {
        foreach (var steamRoot in probes.SteamRoots)
        {
            var steamHit = this.TryProbeSteam(steamRoot);
            if (steamHit is not null)
            {
                return steamHit;
            }
        }

        foreach (var manifestRoot in probes.EpicManifestRoots)
        {
            var epicHit = this.TryProbeEpic(manifestRoot);
            if (epicHit is not null)
            {
                return epicHit;
            }
        }

        return null;
    }

    private RocketLeagueInstall? TryProbeSteam(string steamRoot)
    {
        var libraryFoldersFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersFile))
        {
            LogSteamVdfMissing(logger, libraryFoldersFile, null);
            return null;
        }

        var vdfText = File.ReadAllText(libraryFoldersFile);
        foreach (var libraryPath in EnumerateSteamLibraryPathsContainingApp(vdfText, RocketLeagueSteamAppId))
        {
            var installPath = Path.Combine(libraryPath, "steamapps", "common", "rocketleague");
            if (Directory.Exists(installPath))
            {
                LogSteamFound(logger, installPath, null);
                return new RocketLeagueInstall(installPath, RocketLeagueInstallSource.Steam);
            }

            LogSteamInstallDirMissing(logger, libraryPath, RocketLeagueSteamAppId, installPath, null);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraryPathsContainingApp(string vdfText, string appId)
    {
        var appIdPattern = new Regex($@"""{Regex.Escape(appId)}""\s+""\d+""");

        foreach (Match libraryMatch in LibraryEntryPattern().Matches(vdfText))
        {
            var body = libraryMatch.Groups["body"].Value;
            var pathMatch = LibraryPathPattern().Match(body);
            if (!pathMatch.Success)
            {
                continue;
            }

            var appsMatch = AppsBlockPattern().Match(body);
            if (!appsMatch.Success)
            {
                continue;
            }

            if (appIdPattern.IsMatch(appsMatch.Groups["apps"].Value))
            {
                // VDF paths use backslash escaping (\\); replace to get actual path separators.
                yield return pathMatch.Groups["path"].Value.Replace("\\\\", "\\");
            }
        }
    }

    private RocketLeagueInstall? TryProbeEpic(string manifestRoot)
    {
        if (!Directory.Exists(manifestRoot))
        {
            LogEpicManifestRootMissing(logger, manifestRoot, null);
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(manifestRoot, "*.item", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (!doc.RootElement.TryGetProperty("DisplayName", out var displayName))
                {
                    continue;
                }

                if (!string.Equals(displayName.GetString(), "Rocket League", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!doc.RootElement.TryGetProperty("InstallLocation", out var installLocation))
                {
                    continue;
                }

                var path = installLocation.GetString();
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    LogEpicFound(logger, path, null);
                    return new RocketLeagueInstall(path, RocketLeagueInstallSource.Epic);
                }
            }
            catch (JsonException ex)
            {
                LogEpicManifestUnparseable(logger, file, ex);
            }
        }

        return null;
    }

    public sealed record Probes
    {
        public required IReadOnlyList<string> SteamRoots { get; init; }

        public required IReadOnlyList<string> EpicManifestRoots { get; init; }

        public static Probes Default()
        {
            var steamRoots = new List<string>();
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf86))
            {
                steamRoots.Add(Path.Combine(pf86, "Steam"));
            }

            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(pf))
            {
                steamRoots.Add(Path.Combine(pf, "Steam"));
            }

            var epicRoots = new List<string>();
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                epicRoots.Add(Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests"));
            }

            return new Probes
            {
                SteamRoots = steamRoots,
                EpicManifestRoots = epicRoots
            };
        }
    }
}
