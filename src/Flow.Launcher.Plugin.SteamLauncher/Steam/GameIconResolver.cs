using System.IO;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class GameIconResolver : IGameIconResolver
{
    // Steam stores the small square icon as a SHA-1-content-hash-named .jpg under
    // librarycache/<appid>/; the hash filename is what disambiguates it from other aspect ratios.
    private static readonly Regex HashFileNameRegex = new(
        @"^[0-9a-f]{40}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISteamPathResolver _pathResolver;
    private readonly Lazy<Dictionary<uint, string>> _localIcons;

    public GameIconResolver(ISteamPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
        _localIcons = new Lazy<Dictionary<uint, string>>(LoadLocalIconCache, isThreadSafe: true);
    }

    public string Resolve(uint appId) =>
        _localIcons.Value.TryGetValue(appId, out var path) ? path : CdnCapsuleUrl(appId);

    private static string CdnCapsuleUrl(uint appId) =>
        $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appId}/capsule_231x87.jpg";

    private Dictionary<uint, string> LoadLocalIconCache()
    {
        var icons = new Dictionary<uint, string>();
        try
        {
            var libCacheDir = Path.Combine(_pathResolver.GetSteamInstallPath(), "appcache", "librarycache");
            if (!Directory.Exists(libCacheDir)) return icons;

            foreach (var file in Directory.EnumerateFiles(libCacheDir, "*_icon.*"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var underscoreIdx = name.IndexOf('_', StringComparison.Ordinal);
                if (underscoreIdx > 0
                    && uint.TryParse(name.AsSpan(0, underscoreIdx), out var id))
                {
                    icons.TryAdd(id, file);
                }
            }

            foreach (var subdir in Directory.EnumerateDirectories(libCacheDir))
            {
                var dirName = Path.GetFileName(subdir);
                if (!uint.TryParse(dirName, out var id) || icons.ContainsKey(id))
                    continue;

                var iconPath = FindBestIconInSubdir(subdir);
                if (iconPath is not null)
                    icons[id] = iconPath;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SteamPathNotFoundException) { }
        return icons;
    }

    private static string? FindBestIconInSubdir(string subdir)
    {
        try
        {
            FileInfo? bestHash = null;
            foreach (var file in new DirectoryInfo(subdir).EnumerateFiles())
            {
                if (!file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    && !file.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                var stem = Path.GetFileNameWithoutExtension(file.Name);
                if (!HashFileNameRegex.IsMatch(stem)) continue;

                if (bestHash is null || file.Length < bestHash.Length)
                    bestHash = file;
            }
            if (bestHash is not null) return bestHash.FullName;

            string[] fallbacks = ["icon.jpg", "library_icon.jpg", "header.jpg", "capsule_184x69.jpg", "library_600x900.jpg"];
            foreach (var name in fallbacks)
            {
                var path = Path.Combine(subdir, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
