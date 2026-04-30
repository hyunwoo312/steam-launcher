using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class OwnedGamesServiceTests
{
    private static OwnedGamesService Build(
        ISteamWebApiClient? client = null,
        ICacheStore? cache = null,
        PluginSettings? settings = null)
    {
        client ??= Substitute.For<ISteamWebApiClient>();
        cache ??= new MemoryCacheStore(CachePolicies.Default, persistenceDir: null);
        settings ??= new PluginSettings { SteamId64 = "76561198000000001" };
        return new OwnedGamesService(client, cache, settings);
    }

    [Fact]
    public async Task GetOwnedAppIdsAsync_NoSteamIdConfigured_ReturnsEmpty()
    {
        var service = Build(settings: new PluginSettings { SteamId64 = null });

        var ids = await service.GetOwnedAppIdsAsync(CancellationToken.None);

        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOwnedAppIdsAsync_HappyPath_ReturnsAppIdSet()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(2,
            [
                new OwnedGame(730, "CS2", 120, null, null),
                new OwnedGame(570, "Dota 2", 0, null, null)
            ])));
        var service = Build(client: client);

        var ids = await service.GetOwnedAppIdsAsync(CancellationToken.None);

        ids.Should().BeEquivalentTo(new[] { 730u, 570u });
    }

    [Fact]
    public async Task GetOwnedAppIdsAsync_SecondCall_UsesCache()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(1,
            [new OwnedGame(730, "CS2", 120, null, null)])));
        var service = Build(client: client);

        await service.GetOwnedAppIdsAsync(CancellationToken.None);
        await service.GetOwnedAppIdsAsync(CancellationToken.None);

        await client.Received(1).GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOwnedAppIdsAsync_ApiReturnsNull_ReturnsEmptySet()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns((OwnedGamesResponse?)null);
        var service = Build(client: client);

        var ids = await service.GetOwnedAppIdsAsync(CancellationToken.None);

        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOwnedGamesAsync_ReturnsSummariesIncludingLastPlayed()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(1,
            [new OwnedGame(730, "CS2", 120, null, 1714000000)])));
        var service = Build(client: client);

        var games = await service.GetOwnedGamesAsync(CancellationToken.None);

        games.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                AppId = 730u,
                Name = "CS2",
                PlaytimeMinutes = 120L
            });
        games[0].LastPlayed.Should().NotBeNull();
    }
}
