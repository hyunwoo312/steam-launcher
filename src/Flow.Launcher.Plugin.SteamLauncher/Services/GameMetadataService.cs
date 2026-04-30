using System.Globalization;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class GameMetadataService(
    ISteamWebApiClient client,
    ICacheStore cache) : IGameMetadataService
{
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
        if (cache.TryGet<Json.AppReviewsResponse>(CachePolicies.ReviewScore, key, out var cached))
            return cached;
        if (cache.HasRecentFailure(CachePolicies.ReviewScore, key)) return null;

        var resp = await client.GetAppReviewsAsync(appId, ct).ConfigureAwait(false);
        if (resp?.QuerySummary is null)
        {
            cache.SetFailure(CachePolicies.ReviewScore, key);
            return null;
        }
        cache.Set(CachePolicies.ReviewScore, key, resp);
        return resp;
    }

    private async Task<Json.AppDetailsEnvelope?> GetDetailsAsync(uint appId, string key, CancellationToken ct)
    {
        if (cache.TryGet<Json.AppDetailsEnvelope>(CachePolicies.AppDetails, key, out var cached))
            return cached;
        if (cache.HasRecentFailure(CachePolicies.AppDetails, key)) return null;

        var resp = await client.GetAppDetailsAsync(appId, ct).ConfigureAwait(false);
        if (resp?.Success != true || resp.Data is null)
        {
            cache.SetFailure(CachePolicies.AppDetails, key);
            return null;
        }
        cache.Set(CachePolicies.AppDetails, key, resp);
        return resp;
    }
}
