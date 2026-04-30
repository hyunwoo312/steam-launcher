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
}
