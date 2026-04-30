using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class StoreSearchService(
    ISteamWebApiClient client,
    IOwnedGamesService ownedGames,
    ICacheStore cache,
    PluginSettings settings) : IStoreSearchService
{
    public async Task<IReadOnlyList<StoreGame>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var key = NormalizeKey(query);
        if (cache.TryGet<List<StoreGame>>(CachePolicies.Search, key, out var cached))
            return cached;

        if (cache.HasRecentFailure(CachePolicies.Search, key))
            return [];

        var response = await client
            .SearchStoreAsync(query, settings.PreferredCountryCode ?? "us", ct)
            .ConfigureAwait(false);
        if (response?.Items is null)
        {
            cache.SetFailure(CachePolicies.Search, key);
            return [];
        }

        var ownedSummaries = await ownedGames.GetOwnedGamesAsync(ct).ConfigureAwait(false);
        var ownedByAppId = ownedSummaries.ToDictionary(g => g.AppId);

        var results = response.Items
            .Select(item =>
            {
                var owned = ownedByAppId.TryGetValue(item.Id, out var summary) ? summary : null;
                return new StoreGame
                {
                    AppId = item.Id,
                    Name = item.Name ?? $"App {item.Id}",
                    IconUrl = item.TinyImage,
                    PriceUsd = item.Price?.FinalCents is { } cents ? cents / 100m : null,
                    DiscountPercent = item.Price?.DiscountPercent is > 0 ? item.Price.DiscountPercent : null,
                    IsOwned = owned is not null,
                    LastPlayed = owned?.LastPlayed
                };
            })
            .ToList();

        cache.Set(CachePolicies.Search, key, results);
        return results;
    }

    private static string NormalizeKey(string query) => query.Trim().ToLowerInvariant();
}
