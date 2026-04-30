using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class OwnedGamesService(
    ISteamWebApiClient client,
    ICacheStore cache,
    PluginSettings settings) : IOwnedGamesService
{
    private const string SummariesCacheKey = "owned_summaries";

    public async Task<IReadOnlySet<uint>> GetOwnedAppIdsAsync(CancellationToken ct)
    {
        var summaries = await GetOwnedGamesAsync(ct).ConfigureAwait(false);
        return summaries.Select(s => s.AppId).ToHashSet();
    }

    public async Task<IReadOnlyList<OwnedGameSummary>> GetOwnedGamesAsync(CancellationToken ct)
    {
        if (!ulong.TryParse(settings.SteamId64, out var steamId)) return [];

        if (cache.TryGet<List<OwnedGameSummary>>(CachePolicies.OwnedGames, SummariesCacheKey, out var cached))
            return cached;

        if (cache.HasRecentFailure(CachePolicies.OwnedGames, SummariesCacheKey))
            return [];

        var response = await client.GetOwnedGamesAsync(steamId, ct).ConfigureAwait(false);
        var games = response?.Response?.Games ?? [];
        if (response is null)
        {
            cache.SetFailure(CachePolicies.OwnedGames, SummariesCacheKey);
            return [];
        }

        var summaries = games
            .Select(g => new OwnedGameSummary(
                g.AppId,
                g.Name ?? $"App {g.AppId}",
                g.PlaytimeForever,
                g.RtimeLastPlayed is > 0 ? DateTimeOffset.FromUnixTimeSeconds(g.RtimeLastPlayed.Value) : null))
            .ToList();

        cache.Set(CachePolicies.OwnedGames, SummariesCacheKey, summaries);
        return summaries;
    }
}
