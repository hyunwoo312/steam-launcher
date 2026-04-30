using System.IO;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class SteamPathResolver(IRegistryReader registry, IVdfParser parser) : ISteamPathResolver
{
    private const string SteamRegistryKey = @"Software\Valve\Steam";
    private const string SteamRegistryValue = "SteamPath";

    public string GetSteamInstallPath()
    {
        var raw = registry.ReadCurrentUserString(SteamRegistryKey, SteamRegistryValue);
        if (string.IsNullOrWhiteSpace(raw))
            throw new SteamPathNotFoundException();

        // Steam writes forward slashes into HKCU on Windows; normalize for Path.Combine.
        return raw.Replace('/', '\\');
    }

    public ulong? GetActiveSteamId64()
    {
        try
        {
            var path = Path.Combine(GetSteamInstallPath(), "config", "loginusers.vdf");
            if (!File.Exists(path)) return null;

            var doc = parser.ParseFile(path);
            var users = doc.GetObject("users");
            if (users is null) return null;

            ulong? firstUser = null;
            foreach (var (key, value) in users.Children)
            {
                if (!ulong.TryParse(key, out var steamId)) continue;
                firstUser ??= steamId;
                if (value is VdfNode.Object obj && obj.GetString("MostRecent") == "1")
                    return steamId;
            }
            return firstUser;
        }
        catch (IOException) { return null; }
        catch (Vdf.VdfParseException) { return null; }
        catch (SteamPathNotFoundException) { return null; }
    }

    public string? GetActivePersonaName()
    {
        try
        {
            var path = Path.Combine(GetSteamInstallPath(), "config", "loginusers.vdf");
            if (!File.Exists(path)) return null;

            var doc = parser.ParseFile(path);
            var users = doc.GetObject("users");
            if (users is null) return null;

            string? firstName = null;
            foreach (var (key, value) in users.Children)
            {
                if (!ulong.TryParse(key, out _)) continue;
                if (value is not VdfNode.Object obj) continue;
                firstName ??= obj.GetString("PersonaName");
                if (obj.GetString("MostRecent") == "1")
                    return obj.GetString("PersonaName");
            }
            return firstName;
        }
        catch (IOException) { return null; }
        catch (Vdf.VdfParseException) { return null; }
        catch (SteamPathNotFoundException) { return null; }
    }

    public IReadOnlyList<string> GetLibraryPaths()
    {
        var steamRoot = GetSteamInstallPath();
        var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(libraryFoldersPath))
            return [steamRoot];

        var parsed = parser.ParseFile(libraryFoldersPath);
        var libraryFolders = parsed.GetObject("libraryfolders");
        if (libraryFolders is null)
            return [steamRoot];

        var paths = new List<string>();
        foreach (var (_, child) in libraryFolders.Children)
        {
            if (child is VdfNode.Object obj && obj.GetString("path") is { } path)
                paths.Add(path);
        }

        if (paths.Count == 0)
            paths.Add(steamRoot);

        return paths;
    }
}
