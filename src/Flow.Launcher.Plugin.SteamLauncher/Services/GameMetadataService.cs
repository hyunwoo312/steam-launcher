using System.Globalization;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class GameMetadataService : IGameMetadataService, IDisposable
{
    /// <summary>
    /// Ceiling on storefront requests in flight at once. Callers fan out over whole
    /// libraries and every app costs two requests (reviews + details); past roughly 200
    /// requests in 5 minutes Steam answers 429, which caches as a failure and leaves
    /// rows without review data until the failure TTL lapses.
    /// </summary>
    private const int DefaultMaxConcurrentFetches = 6;

    private readonly ISteamWebApiClient _client;
    private readonly ICacheStore _cache;
    private readonly SemaphoreSlim _fetchGate;

    public GameMetadataService(
        ISteamWebApiClient client,
        ICacheStore cache,
        int maxConcurrentFetches = DefaultMaxConcurrentFetches)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentFetches, 1);
        _client = client;
        _cache = cache;
        _fetchGate = new SemaphoreSlim(maxConcurrentFetches, maxConcurrentFetches);
    }

    public void Dispose() => _fetchGate.Dispose();

    public async Task<GameMetadata> GetAsync(uint appId, CancellationToken ct)
    {
        var key = appId.ToString(CultureInfo.InvariantCulture);

        var reviewsTask = GetReviewsAsync(appId, key, ct);
        var detailsTask = GetDetailsAsync(appId, key, ct);
        await Task.WhenAll(reviewsTask, detailsTask).ConfigureAwait(false);

        var reviews = await reviewsTask.ConfigureAwait(false);
        var details = await detailsTask.ConfigureAwait(false);

        return new GameMetadata
        {
            ReviewSummary = reviews?.QuerySummary?.ReviewScoreDesc,
            ReviewCount = reviews?.QuerySummary?.TotalReviews,
            ReleaseDate = details?.Data?.ReleaseDate?.Date,
            Developer = details?.Data?.Developers?.FirstOrDefault(),
            CategoryIds = details?.Data?.Categories?.Select(c => c.Id).ToList()
                          ?? (IReadOnlyList<int>)Array.Empty<int>()
        };
    }

    private async Task<Json.AppReviewsResponse?> GetReviewsAsync(uint appId, string key, CancellationToken ct)
    {
        if (_cache.TryGet<Json.AppReviewsResponse>(CachePolicies.ReviewScore, key, out var cached))
            return cached;
        if (_cache.HasRecentFailure(CachePolicies.ReviewScore, key)) return null;

        Json.AppReviewsResponse? resp;
        await _fetchGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check: another caller may have filled this key while we queued.
            if (_cache.TryGet<Json.AppReviewsResponse>(CachePolicies.ReviewScore, key, out var filled))
                return filled;
            if (_cache.HasRecentFailure(CachePolicies.ReviewScore, key)) return null;

            resp = await _client.GetAppReviewsAsync(appId, ct).ConfigureAwait(false);
        }
        finally
        {
            _fetchGate.Release();
        }

        if (resp?.QuerySummary is null)
        {
            _cache.SetFailure(CachePolicies.ReviewScore, key);
            return null;
        }
        _cache.Set(CachePolicies.ReviewScore, key, resp);
        return resp;
    }

    private async Task<Json.AppDetailsEnvelope?> GetDetailsAsync(uint appId, string key, CancellationToken ct)
    {
        if (_cache.TryGet<Json.AppDetailsEnvelope>(CachePolicies.AppDetails, key, out var cached))
            return cached;
        if (_cache.HasRecentFailure(CachePolicies.AppDetails, key)) return null;

        Json.AppDetailsEnvelope? resp;
        await _fetchGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGet<Json.AppDetailsEnvelope>(CachePolicies.AppDetails, key, out var filled))
                return filled;
            if (_cache.HasRecentFailure(CachePolicies.AppDetails, key)) return null;

            resp = await _client.GetAppDetailsAsync(appId, ct).ConfigureAwait(false);
        }
        finally
        {
            _fetchGate.Release();
        }

        if (resp?.Success != true || resp.Data is null)
        {
            _cache.SetFailure(CachePolicies.AppDetails, key);
            return null;
        }
        _cache.Set(CachePolicies.AppDetails, key, resp);
        return resp;
    }
}
