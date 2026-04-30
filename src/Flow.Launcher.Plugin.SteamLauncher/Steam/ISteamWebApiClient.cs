using Flow.Launcher.Plugin.SteamLauncher.Json;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public interface ISteamWebApiClient
{
    bool HasApiKey { get; }
    Task<bool> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Public endpoint — no API key required. Hits the storefront search API.
    /// Returns null if the network call failed; throws on programmer errors.
    /// </summary>
    Task<StoreSearchResponse?> SearchStoreAsync(string query, string countryCode, CancellationToken ct);

    /// <summary>Requires API key. Returns null if no key configured or call fails.</summary>
    Task<OwnedGamesResponse?> GetOwnedGamesAsync(ulong steamId64, CancellationToken ct);

    /// <summary>
    /// Resolves persona summaries in batches of 100 (Steam's documented per-request cap).
    /// Returns null if the API key is missing OR any chunk fails — partial-merge would mask outages.
    /// </summary>
    Task<PlayerSummariesResponse?> GetPlayerSummariesAsync(IEnumerable<ulong> steamIds, CancellationToken ct);

    /// <summary>Requires API key. Returns null if no key configured or call fails.</summary>
    Task<SteamLevelResponse?> GetSteamLevelAsync(ulong steamId64, CancellationToken ct);

    /// <summary>Requires API key. Returns null if no key configured or call fails.</summary>
    Task<RecentlyPlayedResponse?> GetRecentlyPlayedAsync(ulong steamId64, CancellationToken ct);

    /// <summary>Public — no key required. Steam community review summary for an app.</summary>
    Task<AppReviewsResponse?> GetAppReviewsAsync(uint appId, CancellationToken ct);

    /// <summary>Public — no key required. Storefront app details (release date, developers).</summary>
    Task<AppDetailsEnvelope?> GetAppDetailsAsync(uint appId, CancellationToken ct);

    /// <summary>Requires API key. Returns null if no key configured or call fails. Membership only — relationship pre-filtered to "friend".</summary>
    Task<GetFriendListResponse?> GetFriendListAsync(ulong steamId64, CancellationToken ct);
}
