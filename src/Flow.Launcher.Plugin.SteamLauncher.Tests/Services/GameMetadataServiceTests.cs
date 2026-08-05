using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class GameMetadataServiceTests
{
    private static GameMetadataService Build(
        ISteamWebApiClient? client = null,
        ICacheStore? cache = null)
    {
        client ??= Substitute.For<ISteamWebApiClient>();
        cache ??= new MemoryCacheStore(CachePolicies.Default, persistenceDir: null);
        return new GameMetadataService(client, cache);
    }

    [Fact]
    public async Task GetAsync_AppDetailsWithCategories_PopulatesCategoryIds()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetAppDetailsAsync(730u, Arg.Any<CancellationToken>())
            .Returns(new AppDetailsEnvelope(
                Success: true,
                Data: new AppDetailsData(
                    Name: "CS2",
                    IsFree: false,
                    Developers: ["Valve"],
                    ReleaseDate: new AppReleaseDate(false, "21 Aug, 2012"),
                    Categories:
                    [
                        new AppDetailsCategory(1, "Multi-player"),
                        new AppDetailsCategory(36, "Online PvP")
                    ])));
        client.GetAppReviewsAsync(730u, Arg.Any<CancellationToken>())
            .Returns((AppReviewsResponse?)null);

        var service = Build(client: client);

        var meta = await service.GetAsync(730u, CancellationToken.None);

        meta.CategoryIds.Should().Equal(1, 36);
    }

    [Fact]
    public async Task GetAsync_AppDetailsMissingCategories_ReturnsEmptyCategoryIds()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetAppDetailsAsync(440u, Arg.Any<CancellationToken>())
            .Returns(new AppDetailsEnvelope(
                Success: true,
                Data: new AppDetailsData(
                    Name: "TF2",
                    IsFree: true,
                    Developers: ["Valve"],
                    ReleaseDate: null,
                    Categories: null)));
        client.GetAppReviewsAsync(440u, Arg.Any<CancellationToken>())
            .Returns((AppReviewsResponse?)null);

        var service = Build(client: client);

        var meta = await service.GetAsync(440u, CancellationToken.None);

        meta.CategoryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_AppDetailsFails_ReturnsEmptyCategoryIds()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetAppDetailsAsync(99u, Arg.Any<CancellationToken>())
            .Returns((AppDetailsEnvelope?)null);
        client.GetAppReviewsAsync(99u, Arg.Any<CancellationToken>())
            .Returns((AppReviewsResponse?)null);

        var service = Build(client: client);

        var meta = await service.GetAsync(99u, CancellationToken.None);

        meta.CategoryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_PreservesExistingFieldsAlongsideCategoryIds()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetAppDetailsAsync(105600u, Arg.Any<CancellationToken>())
            .Returns(new AppDetailsEnvelope(
                Success: true,
                Data: new AppDetailsData(
                    Name: "Terraria",
                    IsFree: false,
                    Developers: ["Re-Logic"],
                    ReleaseDate: new AppReleaseDate(false, "16 May, 2011"),
                    Categories: [new AppDetailsCategory(38, "Online Co-op")])));
        client.GetAppReviewsAsync(105600u, Arg.Any<CancellationToken>())
            .Returns(new AppReviewsResponse(
                Success: 1,
                QuerySummary: new AppReviewsSummary("Overwhelmingly Positive", 100, 5, 105)));

        var service = Build(client: client);

        var meta = await service.GetAsync(105600u, CancellationToken.None);

        meta.Developer.Should().Be("Re-Logic");
        meta.ReleaseDate.Should().Be("16 May, 2011");
        meta.ReviewSummary.Should().Be("Overwhelmingly Positive");
        meta.ReviewCount.Should().Be(105);
        meta.CategoryIds.Should().Equal(38);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCallers_NeverExceedFetchLimit()
    {
        const int maxConcurrent = 4;
        var client = new ConcurrencyTrackingClient();
        var service = new GameMetadataService(
            client,
            new MemoryCacheStore(CachePolicies.Default, persistenceDir: null),
            maxConcurrentFetches: maxConcurrent);

        var appIds = Enumerable.Range(1, 20).Select(i => (uint)i);
        var results = await Task.WhenAll(appIds.Select(id => service.GetAsync(id, CancellationToken.None)));

        results.Should().HaveCount(20);
        client.PeakConcurrency.Should().BeLessThanOrEqualTo(maxConcurrent);
        client.PeakConcurrency.Should().BeGreaterThan(1, "the gate should throttle, not serialize");
    }

    [Fact]
    public async Task GetAsync_SecondCallForSameApp_IsServedFromCache()
    {
        var client = new ConcurrencyTrackingClient();
        var service = new GameMetadataService(
            client,
            new MemoryCacheStore(CachePolicies.Default, persistenceDir: null));

        await service.GetAsync(730u, CancellationToken.None);
        var callsAfterFirst = client.TotalCalls;
        await service.GetAsync(730u, CancellationToken.None);

        client.TotalCalls.Should().Be(callsAfterFirst);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveFetchLimit()
    {
        var act = () => new GameMetadataService(
            Substitute.For<ISteamWebApiClient>(),
            new MemoryCacheStore(CachePolicies.Default, persistenceDir: null),
            maxConcurrentFetches: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Records how many storefront calls overlap. Hand-rolled rather than mocked because the
    /// assertion needs genuinely concurrent tasks, not synchronously-returned ones.
    /// </summary>
    private sealed class ConcurrencyTrackingClient : ISteamWebApiClient
    {
        private readonly object _lock = new();
        private int _current;

        public int PeakConcurrency { get; private set; }
        public int TotalCalls { get; private set; }

        public bool HasApiKey => true;

        public Task<AppReviewsResponse?> GetAppReviewsAsync(uint appId, CancellationToken ct) =>
            TrackAsync<AppReviewsResponse?>(
                new AppReviewsResponse(1, new AppReviewsSummary("Positive", 10, 1, 11)));

        public Task<AppDetailsEnvelope?> GetAppDetailsAsync(uint appId, CancellationToken ct) =>
            TrackAsync<AppDetailsEnvelope?>(
                new AppDetailsEnvelope(true, new AppDetailsData("Game", false, ["Dev"], null, null)));

        private async Task<T> TrackAsync<T>(T result)
        {
            lock (_lock)
            {
                _current++;
                TotalCalls++;
                if (_current > PeakConcurrency) PeakConcurrency = _current;
            }
            try
            {
                await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
                return result;
            }
            finally
            {
                lock (_lock) { _current--; }
            }
        }

        public Task<bool> CheckHealthAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<StoreSearchResponse?> SearchStoreAsync(string query, string countryCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<OwnedGamesResponse?> GetOwnedGamesAsync(ulong steamId64, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerSummariesResponse?> GetPlayerSummariesAsync(IEnumerable<ulong> steamIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<SteamLevelResponse?> GetSteamLevelAsync(ulong steamId64, CancellationToken ct) => throw new NotSupportedException();
        public Task<RecentlyPlayedResponse?> GetRecentlyPlayedAsync(ulong steamId64, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetFriendListResponse?> GetFriendListAsync(ulong steamId64, CancellationToken ct) => throw new NotSupportedException();
    }
}
