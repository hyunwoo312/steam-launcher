using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface ILocalLibraryService
{
    /// <summary>
    /// All installed games across all configured library folders, with
    /// per-game icon paths and playtime resolved when available.
    /// Sorted by LastPlayed descending (most recent first).
    /// Result is cached in-memory after first call.
    /// </summary>
    Task<IReadOnlyList<InstalledGame>> GetInstalledGamesAsync(CancellationToken ct);
}
