using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class StoreSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        var service = Build(Substitute.For<ISteamWebApiClient>(), null);

        var results = await service.SearchAsync("", CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_HappyPath_BuildsStoreGameList()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.SearchStoreAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoreSearchResponse(
            [
                new StoreSearchItem(730, "Counter-Strike 2", "https://example/cs2.jpg", null,
                    new StoreSearchPrice("USD", 0, 0, 0), null, null, "21 Aug, 2012")
            ], 1));
        var service = Build(client, null);

        var results = await service.SearchAsync("counter strike", CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                AppId = 730u,
                Name = "Counter-Strike 2",
                IconUrl = "https://example/cs2.jpg"
            });
    }

    [Fact]
    public async Task SearchAsync_OwnedAppId_FlagsAsOwned()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.SearchStoreAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoreSearchResponse(
            [
                new StoreSearchItem(730, "CS2", null, null, null, null, null, null),
                new StoreSearchItem(440, "TF2", null, null, null, null, null, null)
            ], 2));
        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>())
            .Returns([new OwnedGameSummary(730, "CS2", 0, null)]);
        var service = Build(client, owned);

        var results = await service.SearchAsync("anything", CancellationToken.None);

        results.Should().HaveCount(2);
        results.First(r => r.AppId == 730u).IsOwned.Should().BeTrue();
        results.First(r => r.AppId == 440u).IsOwned.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_FormatsPriceAsDecimalDollars()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.SearchStoreAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoreSearchResponse(
            [new StoreSearchItem(730, "CS2", null, null,
                new StoreSearchPrice("USD", 1999, 1499, 25), null, null, null)], 1));
        var service = Build(client, null);

        var results = await service.SearchAsync("cs", CancellationToken.None);

        results[0].PriceUsd.Should().Be(14.99m);
        results[0].DiscountPercent.Should().Be(25);
    }

    [Fact]
    public async Task SearchAsync_CachesResultForRepeatCall()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.SearchStoreAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StoreSearchResponse(
            [new StoreSearchItem(730, "CS2", null, null, null, null, null, null)], 1));
        var service = Build(client, null);

        await service.SearchAsync("cs", CancellationToken.None);
        await service.SearchAsync("cs", CancellationToken.None);

        await client.Received(1).SearchStoreAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static StoreSearchService Build(ISteamWebApiClient client, IOwnedGamesService? owned)
    {
        var cache = new MemoryCacheStore(CachePolicies.Default, persistenceDir: null);
        if (owned is null)
        {
            owned = Substitute.For<IOwnedGamesService>();
            owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>())
                .Returns(Array.Empty<OwnedGameSummary>());
        }
        var settings = new PluginSettings { PreferredCountryCode = "us" };
        return new StoreSearchService(client, owned, cache, settings);
    }
}
