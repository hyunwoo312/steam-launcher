using System.IO;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class LocalLibraryService(
    ISteamPathResolver pathResolver,
    IVdfParser parser,
    IGameIconResolver iconResolver,
    Action<string, string, Exception>? logException = null)
    : ILocalLibraryService, IDisposable
{
    // 228980 = Steamworks Common Redistributables (shared runtime, not a user-facing game).
    private static readonly HashSet<uint> HiddenAppIds = [228980];

    private readonly object _cacheLock = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private IReadOnlyList<InstalledGame>? _cache;
    private bool _watchersInitialized;
    private bool _disposed;

    public Task<IReadOnlyList<InstalledGame>> GetInstalledGamesAsync(CancellationToken ct)
    {
        lock (_cacheLock)
        {
            if (_cache is not null)
                return Task.FromResult(_cache);
        }

        var games = LoadAllGames(ct);

        lock (_cacheLock)
        {
            _cache = games;
        }

        return Task.FromResult<IReadOnlyList<InstalledGame>>(games);
    }

    private List<InstalledGame> LoadAllGames(CancellationToken ct)
    {
        var (playtimes, recentPlaytimes) = LoadPlaytimeMaps();

        var games = new List<InstalledGame>();

        var libraryPaths = pathResolver.GetLibraryPaths();
        foreach (var libraryPath in libraryPaths)
        {
            ct.ThrowIfCancellationRequested();

            var steamappsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamappsDir))
                continue;

            foreach (var manifestPath in Directory.EnumerateFiles(steamappsDir, "appmanifest_*.acf"))
            {
                ct.ThrowIfCancellationRequested();

                var game = TryParseManifest(manifestPath, libraryPath, playtimes, recentPlaytimes);
                if (game is not null)
                    games.Add(game);
            }
        }

        EnsureWatchers(libraryPaths);

        games.Sort((a, b) =>
        {
            if (a.LastPlayed is null && b.LastPlayed is null) return 0;
            if (a.LastPlayed is null) return 1;
            if (b.LastPlayed is null) return -1;
            return b.LastPlayed.Value.CompareTo(a.LastPlayed.Value);
        });

        return games;
    }

    private InstalledGame? TryParseManifest(
        string manifestPath,
        string libraryPath,
        IReadOnlyDictionary<uint, long> playtimes,
        IReadOnlyDictionary<uint, long> recentPlaytimes)
    {
        try
        {
            var doc = parser.ParseFile(manifestPath);
            var appState = doc.GetObject("AppState");
            if (appState is null) return null;

            var appIdStr = appState.GetString("appid");
            var name = appState.GetString("name");
            var installDir = appState.GetString("installdir");
            if (appIdStr is null || name is null || installDir is null) return null;
            if (!uint.TryParse(appIdStr, out var appId)) return null;
            if (HiddenAppIds.Contains(appId)) return null;

            DateTimeOffset? lastPlayed = null;
            if (appState.GetString("LastPlayed") is { } lp && long.TryParse(lp, out var unixSec) && unixSec > 0)
                lastPlayed = DateTimeOffset.FromUnixTimeSeconds(unixSec);

            long sizeOnDisk = 0;
            if (appState.GetString("SizeOnDisk") is { } so) long.TryParse(so, out sizeOnDisk);

            uint stateFlags = 0;
            if (appState.GetString("StateFlags") is { } sf) uint.TryParse(sf, out stateFlags);

            return new InstalledGame
            {
                AppId = appId,
                Name = name,
                InstallDir = installDir,
                LibraryPath = libraryPath,
                LastPlayed = lastPlayed,
                SizeOnDiskBytes = sizeOnDisk,
                StateFlags = stateFlags,
                IconPath = iconResolver.Resolve(appId),
                PlaytimeMinutes = playtimes.TryGetValue(appId, out var minutes) ? minutes : null,
                PlaytimeLast2WeeksMinutes = recentPlaytimes.TryGetValue(appId, out var recentMin) ? recentMin : null
            };
        }
        catch (VdfParseException ex)
        {
            logException?.Invoke(nameof(LocalLibraryService), $"Skipping malformed manifest: {manifestPath}", ex);
            return null;
        }
        catch (IOException ex)
        {
            logException?.Invoke(nameof(LocalLibraryService), $"Skipping inaccessible manifest: {manifestPath}", ex);
            return null;
        }
    }

    // Pick the userdata subdir whose localconfig.vdf was most recently modified as the active user.
    private (Dictionary<uint, long> Total, Dictionary<uint, long> Recent2wk) LoadPlaytimeMaps()
    {
        var totals = new Dictionary<uint, long>();
        var recent = new Dictionary<uint, long>();
        try
        {
            var userdataRoot = Path.Combine(pathResolver.GetSteamInstallPath(), "userdata");
            if (!Directory.Exists(userdataRoot))
                return (totals, recent);

            string? bestConfigPath = null;
            DateTime bestMtime = DateTime.MinValue;
            foreach (var userDir in Directory.EnumerateDirectories(userdataRoot))
            {
                var configPath = Path.Combine(userDir, "config", "localconfig.vdf");
                if (!File.Exists(configPath)) continue;

                var mtime = File.GetLastWriteTimeUtc(configPath);
                if (mtime > bestMtime)
                {
                    bestMtime = mtime;
                    bestConfigPath = configPath;
                }
            }

            if (bestConfigPath is null) return (totals, recent);

            var doc = parser.ParseFile(bestConfigPath);
            var apps = doc
                .GetObject("UserLocalConfigStore")
                ?.GetObject("Software")
                ?.GetObject("Valve")
                ?.GetObject("Steam")
                ?.GetObject("apps");
            if (apps is null) return (totals, recent);

            foreach (var (key, value) in apps.Children)
            {
                if (!uint.TryParse(key, out var appId)) continue;
                if (value is not VdfNode.Object appObj) continue;

                if (appObj.GetString("Playtime") is { } pt
                    && long.TryParse(pt, out var minutes) && minutes > 0)
                {
                    totals[appId] = minutes;
                }

                if (appObj.GetString("Playtime2wks") is { } pt2
                    && long.TryParse(pt2, out var recentMinutes) && recentMinutes > 0)
                {
                    recent[appId] = recentMinutes;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (VdfParseException) { }
        catch (SteamPathNotFoundException) { }
        return (totals, recent);
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
            _cache = null;
    }

    private void EnsureWatchers(IReadOnlyList<string> libraryPaths)
    {
        lock (_cacheLock)
        {
            if (_watchersInitialized || _disposed) return;
            _watchersInitialized = true;
        }

        foreach (var libraryPath in libraryPaths)
        {
            var steamappsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamappsDir)) continue;

            try
            {
                var watcher = new FileSystemWatcher(steamappsDir, "appmanifest_*.acf")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                watcher.Created += (_, _) => InvalidateCache();
                watcher.Deleted += (_, _) => InvalidateCache();
                watcher.Renamed += (_, _) => InvalidateCache();
                watcher.Changed += (_, _) => InvalidateCache();
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                logException?.Invoke(nameof(LocalLibraryService), $"Failed to watch {steamappsDir} for library changes", ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
    }
}
