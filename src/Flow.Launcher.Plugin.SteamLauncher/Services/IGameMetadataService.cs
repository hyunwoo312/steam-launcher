using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IGameMetadataService
{
    /// <summary>
    /// Returns the cached metadata for an app — review summary, total review count,
    /// release date, primary developer. Fetches from Steam's storefront on cache miss.
    /// Returns <see cref="GameMetadata.Empty"/> on lookup failure rather than null so
    /// callers can render rows without null-guarding every field.
    /// </summary>
    Task<GameMetadata> GetAsync(uint appId, CancellationToken ct);
}
