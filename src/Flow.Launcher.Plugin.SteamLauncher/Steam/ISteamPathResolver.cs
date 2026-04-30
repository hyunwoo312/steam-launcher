namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public interface ISteamPathResolver
{
    string GetSteamInstallPath();
    IReadOnlyList<string> GetLibraryPaths();

    /// <summary>
    /// Reads <c>config/loginusers.vdf</c> and returns the Steam ID 64 of the most-recent
    /// account (or the only account if there's just one). Null if Steam isn't installed,
    /// the file is missing, or no accounts have ever logged in on this machine.
    /// </summary>
    ulong? GetActiveSteamId64();

    /// <summary>
    /// Reads <c>config/loginusers.vdf</c> and returns the PersonaName of the most-recent
    /// account. Null if not resolvable. May change between runs as the user updates it.
    /// </summary>
    string? GetActivePersonaName();
}

public interface IRegistryReader
{
    string? ReadCurrentUserString(string keyPath, string valueName);
}

public sealed class SteamPathNotFoundException()
    : Exception("Could not locate Steam installation. Is Steam installed?");
