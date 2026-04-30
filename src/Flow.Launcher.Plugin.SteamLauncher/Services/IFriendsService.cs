using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IFriendsService
{
    /// <summary>
    /// Returns the configured user's friends with persona state hydrated.
    /// Empty list when the Steam ID is not configured, the API key is missing, or any call fails.
    /// </summary>
    Task<IReadOnlyList<Friend>> GetFriendsAsync(CancellationToken ct);

    /// <summary>
    /// Pre-fetches the friend list at plugin start so the first user query hits a warm cache.
    /// Errors are swallowed; this is intentionally fire-and-forget.
    /// </summary>
    Task WarmUpAsync(CancellationToken ct);
}
