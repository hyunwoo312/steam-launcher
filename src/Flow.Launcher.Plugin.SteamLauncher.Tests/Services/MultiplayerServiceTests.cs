using FluentAssertions;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class MultiplayerServiceTests
{
    private static MatchResult Hit(int score) => new(true, SearchPrecisionScore.None, [], score);
    private static MatchResult Miss() => new(false, SearchPrecisionScore.None, [], 0);

    private static Friend MakeFriend(ulong id, string name) => new()
    {
        SteamId64 = id,
        PersonaName = name
    };

    private static OwnedGameSummary OwnedGame(uint appId, string name, long playtimeMin = 0, DateTimeOffset? lastPlayed = null) =>
        new(appId, name, playtimeMin, lastPlayed);

    private static OwnedGame FriendOwned(uint appId, string name, int playtimeMin = 0, long? lastPlayed = null) =>
        new(appId, name, playtimeMin, null, lastPlayed);

    private static MultiplayerService Build(
        IFriendsService friends,
        IOwnedGamesService owned,
        ISteamWebApiClient client,
        IGameMetadataService metadata,
        IFuzzyMatcher matcher)
        => new(friends, owned, client, metadata, matcher);

    [Fact]
    public async Task FindSharedAsync_NoFriendMatch_ReturnsNoMatchHint()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>()).Returns(Miss());
        var service = Build(
            friends,
            Substitute.For<IOwnedGamesService>(),
            Substitute.For<ISteamWebApiClient>(),
            Substitute.For<IGameMetadataService>(),
            matcher);

        var result = await service.FindSharedAsync("xyz", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.NoFriendMatch);
    }

    [Fact]
    public async Task FindSharedAsync_FriendOwnedGamesEmpty_ReturnsPrivateHintWithName()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("al", "Alex").Returns(Hit(80));
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(0, [])));
        var service = Build(
            friends,
            Substitute.For<IOwnedGamesService>(),
            client,
            Substitute.For<IGameMetadataService>(),
            matcher);

        var result = await service.FindSharedAsync("al", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.PrivateOrEmpty);
        result.PersonaName.Should().Be("Alex");
    }

    [Fact]
    public async Task FindSharedAsync_FilterReturnsOnlyMultiplayerCategoryGames()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(100));

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            OwnedGame(730, "CS2"),
            OwnedGame(440, "TF2"),
            OwnedGame(105600, "Terraria"),
            OwnedGame(391540, "Undertale")
        });

        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(4, new List<OwnedGame>
            {
                FriendOwned(730, "CS2"),
                FriendOwned(440, "TF2"),
                FriendOwned(105600, "Terraria"),
                FriendOwned(391540, "Undertale"),
                FriendOwned(220, "HL2")
            })));

        var metadata = Substitute.For<IGameMetadataService>();
        metadata.GetAsync(730u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = new[] { MultiplayerCategoryIds.MultiPlayer, MultiplayerCategoryIds.OnlinePvP } });
        metadata.GetAsync(440u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = new[] { MultiplayerCategoryIds.MultiPlayer } });
        metadata.GetAsync(105600u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = new[] { MultiplayerCategoryIds.OnlineCoOp } });
        metadata.GetAsync(391540u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = Array.Empty<int>() });

        var service = Build(friends, owned, client, metadata, matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.Match);
        result.SharedGames.Select(g => g.AppId).Should().BeEquivalentTo(new uint[] { 730, 440, 105600 });
    }

    [Fact]
    public async Task FindSharedAsync_SortsBothPlayedAboveOnePlayedAboveNeitherPlayed()
    {
        // Both-played-a-lot (CS2) > one-played (Portal) > neither-played (CS:S, with stale touch
        // timestamps that the old min(lastPlayed) sort would incorrectly bubble up).
        var now = DateTimeOffset.UtcNow;
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(100));

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            OwnedGame(240, "CSS",  playtimeMin: 0,  lastPlayed: now.AddDays(-2)),
            OwnedGame(620, "Portal2", playtimeMin: 0, lastPlayed: null),
            OwnedGame(730, "CS2",  playtimeMin: 12420, lastPlayed: now.AddDays(-30))
        });

        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(3, new List<OwnedGame>
            {
                FriendOwned(240, "CSS",  playtimeMin: 0,    lastPlayed: now.AddDays(-1).ToUnixTimeSeconds()),
                FriendOwned(620, "Portal2", playtimeMin: 1140, lastPlayed: now.AddDays(-50).ToUnixTimeSeconds()),
                FriendOwned(730, "CS2",  playtimeMin: 10140, lastPlayed: now.AddDays(-40).ToUnixTimeSeconds())
            })));

        var metadata = Substitute.For<IGameMetadataService>();
        var multiplayerMeta = new GameMetadata { CategoryIds = new[] { MultiplayerCategoryIds.MultiPlayer } };
        metadata.GetAsync(Arg.Any<uint>(), Arg.Any<CancellationToken>()).Returns(multiplayerMeta);

        var service = Build(friends, owned, client, metadata, matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.SharedGames.Select(g => g.AppId).Should().Equal(730u, 620u, 240u);
    }

    [Fact]
    public async Task FindSharedAsync_PicksHighestFuzzyScoreWhenMultipleHits()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            MakeFriend(2, "Alex"),
            MakeFriend(3, "Albert"),
            MakeFriend(4, "Sam")
        });
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("al", "Alex").Returns(Hit(80));
        matcher.Match("al", "Albert").Returns(Hit(60));
        matcher.Match("al", "Sam").Returns(Miss());

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<OwnedGameSummary>());

        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(0, [])));

        var service = Build(friends, owned, client, Substitute.For<IGameMetadataService>(), matcher);

        var result = await service.FindSharedAsync("al", CancellationToken.None);

        await client.Received(1).GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>());
        result.PersonaName.Should().Be("Alex");
    }

    [Fact]
    public async Task FindSharedAsync_OwnedGamesCallReturnsNull_ReturnsPrivateHint()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(80));
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns((OwnedGamesResponse?)null);
        var service = Build(friends, Substitute.For<IOwnedGamesService>(), client,
            Substitute.For<IGameMetadataService>(), matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.PrivateOrEmpty);
    }

    [Fact]
    public async Task FindSharedAsync_LostArkRealCategoryShape_IsIncludedInResults()
    {
        // Real category set from Steam appdetails for Lost Ark (1599340), captured 2026-04-29.
        var lostArkCategoryIds = new[]
        {
            2,
            MultiplayerCategoryIds.MultiPlayer,
            MultiplayerCategoryIds.Mmo,
            MultiplayerCategoryIds.SharedSplitScreen,
            MultiplayerCategoryIds.OnlinePvP,
            MultiplayerCategoryIds.CoOp,
            MultiplayerCategoryIds.OnlineCoOp,
            22, 35, 64, 67, 66, 68, 75, 74, 79, 18
        };

        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(100));

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            OwnedGame(1599340, "Lost Ark")
        });

        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(1, [FriendOwned(1599340, "Lost Ark")])));

        var metadata = Substitute.For<IGameMetadataService>();
        metadata.GetAsync(1599340u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = lostArkCategoryIds });

        var service = Build(friends, owned, client, metadata, matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.Match);
        result.SharedGames.Select(g => g.AppId).Should().Contain(1599340u);
    }

    [Fact]
    public async Task FindSharedAsync_MmoOnlyGame_IsIncludedInResults()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(100));

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            OwnedGame(99999, "PureMmo")
        });

        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(1, [FriendOwned(99999, "PureMmo")])));

        var metadata = Substitute.For<IGameMetadataService>();
        metadata.GetAsync(99999u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = new[] { MultiplayerCategoryIds.Mmo } });

        var service = Build(friends, owned, client, metadata, matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.Match);
        result.SharedGames.Select(g => g.AppId).Should().Contain(99999u);
    }

    [Fact]
    public async Task FindSharedAsync_NoIntersectionEvenAfterMultiplayerFilter_ReturnsEmptyMatch()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns([MakeFriend(2, "Alex")]);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("alex", "Alex").Returns(Hit(100));

        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { OwnedGame(391540, "Undertale") });
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetOwnedGamesAsync(2UL, Arg.Any<CancellationToken>())
            .Returns(new OwnedGamesResponse(new OwnedGamesBody(1, [FriendOwned(391540, "Undertale")])));

        var metadata = Substitute.For<IGameMetadataService>();
        metadata.GetAsync(391540u, Arg.Any<CancellationToken>())
            .Returns(new GameMetadata { CategoryIds = Array.Empty<int>() });

        var service = Build(friends, owned, client, metadata, matcher);

        var result = await service.FindSharedAsync("alex", CancellationToken.None);

        result.Outcome.Should().Be(MultiplayerOutcome.Match);
        result.SharedGames.Should().BeEmpty();
    }
}
