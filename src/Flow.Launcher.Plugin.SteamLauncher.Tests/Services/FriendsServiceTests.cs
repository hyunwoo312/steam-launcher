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

public sealed class FriendsServiceTests
{
    private static FriendsService Build(
        ISteamWebApiClient? client = null,
        ICacheStore? cache = null,
        PluginSettings? settings = null)
    {
        client ??= Substitute.For<ISteamWebApiClient>();
        cache ??= new MemoryCacheStore(CachePolicies.Default, persistenceDir: null);
        settings ??= new PluginSettings { SteamId64 = "76561198000000001" };
        return new FriendsService(client, cache, settings);
    }

    [Fact]
    public async Task GetFriendsAsync_NoSteamIdConfigured_ReturnsEmpty()
    {
        var service = Build(settings: new PluginSettings { SteamId64 = null });

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        friends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriendsAsync_HappyPath_ReturnsHydratedFriends()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody(
            [
                new FriendListEntry("76561198000000002", "friend", 1714000000),
                new FriendListEntry("76561198000000003", "friend", 1715000000)
            ])));
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns(new PlayerSummariesResponse(new PlayerSummariesBody(
            [
                new PlayerSummary("76561198000000002", "Alex", "https://example/a.jpg", 1, "CS2", "730", null),
                new PlayerSummary("76561198000000003", "Sam",  "https://example/b.jpg", 0, null,  null,  1714999999L)
            ])));
        var service = Build(client: client);

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        friends.Should().HaveCount(2);
        var alex = friends.First(f => f.SteamId64 == 76561198000000002UL);
        alex.PersonaName.Should().Be("Alex");
        alex.PersonaState.Should().Be(PersonaState.Online);
        alex.CurrentGameAppId.Should().Be(730u);
        alex.CurrentGameName.Should().Be("CS2");
        alex.IsInGame.Should().BeTrue();
        alex.AvatarUrl.Should().Be("https://example/a.jpg");

        var sam = friends.First(f => f.SteamId64 == 76561198000000003UL);
        sam.PersonaState.Should().Be(PersonaState.Offline);
        sam.CurrentGameAppId.Should().BeNull();
        sam.LastLogoffUnix.Should().Be(1714999999L);
    }

    [Fact]
    public async Task GetFriendsAsync_FriendInNonSteamGame_HasNameButNoAppId()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody(
            [new FriendListEntry("76561198000000002", "friend", 1714000000)])));
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns(new PlayerSummariesResponse(new PlayerSummariesBody(
            [new PlayerSummary("76561198000000002", "Alex", null, 1, "Spotify", null, null)])));
        var service = Build(client: client);

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        var alex = friends.Single();
        alex.CurrentGameName.Should().Be("Spotify");
        alex.CurrentGameAppId.Should().BeNull();
        alex.IsInGame.Should().BeFalse();
    }

    [Fact]
    public async Task GetFriendsAsync_SecondCall_UsesCache()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody(
            [new FriendListEntry("76561198000000002", "friend", 1714000000)])));
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns(new PlayerSummariesResponse(new PlayerSummariesBody(
            [new PlayerSummary("76561198000000002", "Alex", null, 1, null, null, null)])));
        var service = Build(client: client);

        await service.GetFriendsAsync(CancellationToken.None);
        await service.GetFriendsAsync(CancellationToken.None);

        await client.Received(1).GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>());
        await client.Received(1).GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFriendsAsync_FriendListFails_ReturnsEmptyAndCachesFailure()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns((GetFriendListResponse?)null);
        var cache = new MemoryCacheStore(CachePolicies.Default, persistenceDir: null);
        var service = Build(client: client, cache: cache);

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        friends.Should().BeEmpty();
        cache.HasRecentFailure(CachePolicies.FriendList, "all").Should().BeTrue();
    }

    [Fact]
    public async Task GetFriendsAsync_SummariesFails_ReturnsEmpty()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody(
            [new FriendListEntry("76561198000000002", "friend", 1714000000)])));
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns((PlayerSummariesResponse?)null);
        var service = Build(client: client);

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        friends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriendsAsync_EmptyFriendList_ReturnsEmptyWithoutFetchingSummaries()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody([])));
        var service = Build(client: client);

        var friends = await service.GetFriendsAsync(CancellationToken.None);

        friends.Should().BeEmpty();
        await client.DidNotReceive().GetPlayerSummariesAsync(
            Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFriendsAsync_PassesFriendIdsToSummariesCall()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetFriendListAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new GetFriendListResponse(new FriendListBody(
            [
                new FriendListEntry("76561198000000002", "friend", 1714000000),
                new FriendListEntry("76561198000000003", "friend", 1715000000)
            ])));
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns(new PlayerSummariesResponse(new PlayerSummariesBody([])));
        IEnumerable<ulong>? capturedIds = null;
        await client.GetPlayerSummariesAsync(
            Arg.Do<IEnumerable<ulong>>(ids => capturedIds = ids.ToList()),
            Arg.Any<CancellationToken>());
        var service = Build(client: client);

        await service.GetFriendsAsync(CancellationToken.None);

        capturedIds.Should().BeEquivalentTo(new[] { 76561198000000002UL, 76561198000000003UL });
    }
}
