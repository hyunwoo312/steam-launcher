using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Security;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class SteamWebApiClient(
    HttpClient http,
    IApiKeyStore keyStore,
    Action<string, string, Exception>? logException = null) : ISteamWebApiClient
{
    private const string ServerInfoEndpoint = "ISteamWebAPIUtil/GetServerInfo/v1/";
    private const string OwnedGamesEndpoint = "IPlayerService/GetOwnedGames/v1/";
    private const string PlayerSummariesEndpoint = "ISteamUser/GetPlayerSummaries/v2/";
    private const string SteamLevelEndpoint = "IPlayerService/GetSteamLevel/v1/";
    private const string RecentlyPlayedEndpoint = "IPlayerService/GetRecentlyPlayedGames/v1/";
    private const string FriendListEndpoint = "ISteamUser/GetFriendList/v1/";
    private const int PlayerSummariesChunkSize = 100;

    private static readonly Uri StoreSearchUri = new("https://store.steampowered.com/api/storesearch/");
    private static readonly Uri AppDetailsUri = new("https://store.steampowered.com/api/appdetails");
    private const string AppReviewsHost = "https://store.steampowered.com";

    public bool HasApiKey => keyStore.IsConfigured;

    public async Task<bool> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(ServerInfoEndpoint, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logException?.Invoke(nameof(SteamWebApiClient),
                    $"Steam API health check returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    new HttpRequestException($"HTTP {(int)response.StatusCode}"));
                return false;
            }

            var payload = await response.Content
                .ReadFromJsonAsync(SteamJsonContext.Default.HealthCheckResponse, ct)
                .ConfigureAwait(false);

            return payload?.Response?.ServerTime is > 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            logException?.Invoke(nameof(SteamWebApiClient), "Steam API health check failed (network error)", ex);
            return false;
        }
        catch (JsonException ex)
        {
            logException?.Invoke(nameof(SteamWebApiClient), "Steam API health check returned invalid JSON", ex);
            return false;
        }
    }

    public async Task<StoreSearchResponse?> SearchStoreAsync(string query, string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var url = $"{StoreSearchUri}?term={Uri.EscapeDataString(query)}&l=english&cc={Uri.EscapeDataString(countryCode)}";
        return await GetAsync(new Uri(url), SteamJsonContext.Default.StoreSearchResponse, ct).ConfigureAwait(false);
    }

    public async Task<OwnedGamesResponse?> GetOwnedGamesAsync(ulong steamId64, CancellationToken ct)
    {
        var key = keyStore.Load();
        if (string.IsNullOrEmpty(key)) return null;

        var url = $"{OwnedGamesEndpoint}?key={key}&steamid={steamId64}&include_appinfo=1&include_played_free_games=1";
        return await GetAsync(new Uri(url, UriKind.Relative), SteamJsonContext.Default.OwnedGamesResponse, ct).ConfigureAwait(false);
    }

    public async Task<PlayerSummariesResponse?> GetPlayerSummariesAsync(IEnumerable<ulong> steamIds, CancellationToken ct)
    {
        var key = keyStore.Load();
        if (string.IsNullOrEmpty(key)) return null;

        var distinctIds = steamIds.Distinct().ToList();
        if (distinctIds.Count == 0) return null;

        var merged = new List<PlayerSummary>(distinctIds.Count);
        foreach (var chunk in Chunk(distinctIds, PlayerSummariesChunkSize))
        {
            var ids = string.Join(",", chunk.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            var url = $"{PlayerSummariesEndpoint}?key={key}&steamids={Uri.EscapeDataString(ids)}";
            var chunkResponse = await GetAsync(
                new Uri(url, UriKind.Relative),
                SteamJsonContext.Default.PlayerSummariesResponse,
                ct).ConfigureAwait(false);

            if (chunkResponse?.Response?.Players is null) return null;
            merged.AddRange(chunkResponse.Response.Players);
        }

        return new PlayerSummariesResponse(new PlayerSummariesBody(merged));
    }

    public async Task<SteamLevelResponse?> GetSteamLevelAsync(ulong steamId64, CancellationToken ct)
    {
        var key = keyStore.Load();
        if (string.IsNullOrEmpty(key)) return null;

        var url = $"{SteamLevelEndpoint}?key={key}&steamid={steamId64}";
        return await GetAsync(new Uri(url, UriKind.Relative), SteamJsonContext.Default.SteamLevelResponse, ct).ConfigureAwait(false);
    }

    public async Task<RecentlyPlayedResponse?> GetRecentlyPlayedAsync(ulong steamId64, CancellationToken ct)
    {
        var key = keyStore.Load();
        if (string.IsNullOrEmpty(key)) return null;

        var url = $"{RecentlyPlayedEndpoint}?key={key}&steamid={steamId64}&count=10";
        return await GetAsync(new Uri(url, UriKind.Relative), SteamJsonContext.Default.RecentlyPlayedResponse, ct).ConfigureAwait(false);
    }

    public async Task<GetFriendListResponse?> GetFriendListAsync(ulong steamId64, CancellationToken ct)
    {
        var key = keyStore.Load();
        if (string.IsNullOrEmpty(key)) return null;

        var url = $"{FriendListEndpoint}?key={key}&steamid={steamId64}&relationship=friend";
        return await GetAsync(new Uri(url, UriKind.Relative), SteamJsonContext.Default.GetFriendListResponse, ct).ConfigureAwait(false);
    }

    public async Task<AppReviewsResponse?> GetAppReviewsAsync(uint appId, CancellationToken ct)
    {
        var url = $"{AppReviewsHost}/appreviews/{appId}?json=1&language=all&purchase_type=all&num_per_page=0";
        return await GetAsync(new Uri(url), SteamJsonContext.Default.AppReviewsResponse, ct).ConfigureAwait(false);
    }

    public async Task<AppDetailsEnvelope?> GetAppDetailsAsync(uint appId, CancellationToken ct)
    {
        var url = $"{AppDetailsUri}?appids={appId}&filters=basic,release_date,developers,categories";
        var dict = await GetAsync(
            new Uri(url),
            SteamJsonContext.Default.DictionaryStringAppDetailsEnvelope,
            ct).ConfigureAwait(false);
        return dict is not null
            && dict.TryGetValue(appId.ToString(CultureInfo.InvariantCulture), out var envelope)
            ? envelope
            : null;
    }

    private async Task<T?> GetAsync<T>(
        Uri uri,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(uri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logException?.Invoke(nameof(SteamWebApiClient),
                    $"Steam API request to {Redact(uri)} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    new HttpRequestException($"HTTP {(int)response.StatusCode}"));
                return null;
            }

            return await response.Content.ReadFromJsonAsync(typeInfo, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            logException?.Invoke(nameof(SteamWebApiClient), $"Steam API request to {Redact(uri)} failed (network error)", ex);
            return null;
        }
        catch (JsonException ex)
        {
            logException?.Invoke(nameof(SteamWebApiClient), $"Steam API response from {Redact(uri)} was not valid JSON", ex);
            return null;
        }
    }

    private static string Redact(Uri uri)
    {
        var text = uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Path) : uri.OriginalString;
        var queryStart = text.IndexOf('?', StringComparison.Ordinal);
        return queryStart >= 0 ? text[..queryStart] : text;
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }
}
