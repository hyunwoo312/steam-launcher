using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IStoreSearchService
{
    Task<IReadOnlyList<StoreGame>> SearchAsync(string query, CancellationToken ct);
}
