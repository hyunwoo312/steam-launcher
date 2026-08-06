using FluentAssertions;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class QueryDispatcherTests
{
    private static readonly Action<string, string, Exception> NoLog = (_, _, _) => { };

    private static InstalledGame Game(
        uint id,
        string name,
        long? playtimeMin = null,
        long? recent = null,
        uint stateFlags = 4) => new()
    {
        AppId = id,
        Name = name,
        InstallDir = name,
        LibraryPath = @"C:\steam",
        SizeOnDiskBytes = 35_000_000_000,
        StateFlags = stateFlags,
        PlaytimeMinutes = playtimeMin,
        PlaytimeLast2WeeksMinutes = recent
    };

    private static MatchResult Hit(int score) => new(true, SearchPrecisionScore.None, [], score);

    private static MatchResult Miss() => new(false, SearchPrecisionScore.None, [], 0);

    private static QueryDispatcher BuildDispatcher(
        ILocalLibraryService library,
        IFuzzyMatcher matcher,
        IStoreSearchService? storeSearch = null,
        IOwnedGamesService? owned = null,
        IUserProfileService? profile = null,
        IApiKeyStore? keyStore = null,
        PluginSettings? settings = null,
        Action? saveSettings = null,
        IGameIconResolver? iconResolver = null,
        IGameMetadataService? metadata = null,
        IFriendsService? friends = null,
        IAvatarCache? avatarCache = null,
        IStatusService? status = null,
        IAccountSwitcherService? accountSwitcher = null,
        IMultiplayerService? multiplayer = null,
        Action<string, string>? showToast = null,
        Action<string, bool>? changeQuery = null,
        string actionKeyword = "st",
        Func<ulong?>? getActiveSteamId = null,
        Action? invalidateUserCaches = null,
        Func<bool>? isNetworkAvailable = null,
        Func<bool>? isBigPictureRunning = null)
    {
        if (metadata is null)
        {
            metadata = Substitute.For<IGameMetadataService>();
            metadata.GetAsync(Arg.Any<uint>(), Arg.Any<CancellationToken>()).Returns(GameMetadata.Empty);
        }
        return new(
            library,
            matcher,
            storeSearch ?? Substitute.For<IStoreSearchService>(),
            owned ?? Substitute.For<IOwnedGamesService>(),
            profile ?? Substitute.For<IUserProfileService>(),
            keyStore ?? Substitute.For<IApiKeyStore>(),
            settings ?? new PluginSettings(),
            saveSettings ?? (() => { }),
            iconResolver ?? Substitute.For<IGameIconResolver>(),
            metadata,
            friends ?? Substitute.For<IFriendsService>(),
            avatarCache ?? Substitute.For<IAvatarCache>(),
            status ?? Substitute.For<IStatusService>(),
            accountSwitcher ?? Substitute.For<IAccountSwitcherService>(),
            multiplayer ?? Substitute.For<IMultiplayerService>(),
            showToast,
            changeQuery,
            actionKeyword,
            getActiveSteamId ?? (() => null),
            invalidateUserCaches ?? (() => { }),
            localPersonaName: null,
            "icon.png",
            NoLog,
            isNetworkAvailable ?? (() => true),
            isBigPictureRunning);
    }

    [Fact]
    public async Task Empty_query_returns_all_games_in_service_order()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo"), Game(3, "Charlie")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        var dispatcher = BuildDispatcher(library, matcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results.Should().HaveCount(4);
        results[0].Title.Should().Be("Launch Steam");
        results.Skip(1).Select(r => r.Title).Should().Equal("Alpha", "Bravo", "Charlie");
        results[1].Preview.Description.Should().Contain("Alpha (1)");
        results[1].Preview.Description.Should().Contain(@"Install path: C:\steam\steamapps\common\Alpha");
        matcher.DidNotReceiveWithAnyArgs().Match(default!, default!);
    }

    [Fact]
    public async Task FastEmpty_returns_launch_row_and_library_without_remote_enrichment()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo")]));
        var storeSearch = Substitute.For<IStoreSearchService>();
        var metadata = Substitute.For<IGameMetadataService>();
        var friends = Substitute.For<IFriendsService>();
        var dispatcher = BuildDispatcher(
            library,
            Substitute.For<IFuzzyMatcher>(),
            storeSearch: storeSearch,
            metadata: metadata,
            friends: friends);

        var results = await dispatcher.BuildFastEmptyResultsAsync(CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Launch Steam", "Alpha", "Bravo");
        await storeSearch.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
        await metadata.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        await friends.DidNotReceiveWithAnyArgs().GetFriendsAsync(default);
    }

    [Fact]
    public async Task Filter_keeps_only_matched_games()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo"), Game(3, "Charlie")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("a", "Alpha").Returns(Hit(80));
        matcher.Match("a", "Bravo").Returns(Hit(40));
        matcher.Match("a", "Charlie").Returns(Miss());
        var dispatcher = BuildDispatcher(library, matcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.LibraryFilter("a"), CancellationToken.None);

        results.Should().HaveCount(2);
        results.Select(r => r.Title).Should().NotContain("Charlie");
    }

    [Fact]
    public async Task Filter_orders_by_descending_score()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Lower"), Game(2, "Higher")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("x", "Lower").Returns(Hit(20));
        matcher.Match("x", "Higher").Returns(Hit(90));
        var dispatcher = BuildDispatcher(library, matcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.LibraryFilter("x"), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Higher", "Lower");
    }

    [Fact]
    public async Task FastFilter_returns_library_matches_without_waiting_for_remote_enrichment()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Slay the Spire"), Game(2, "Slay the Spire 2"), Game(3, "Other")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("slay", "Slay the Spire").Returns(Hit(80));
        matcher.Match("slay", "Slay the Spire 2").Returns(Hit(100));
        matcher.Match("slay", "Other").Returns(Miss());
        var storeSearch = Substitute.For<IStoreSearchService>();
        var metadata = Substitute.For<IGameMetadataService>();
        var friends = Substitute.For<IFriendsService>();
        var dispatcher = BuildDispatcher(
            library,
            matcher,
            storeSearch: storeSearch,
            metadata: metadata,
            friends: friends);

        var results = await dispatcher.BuildFastFilteredResultsAsync("slay", CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Slay the Spire 2", "Slay the Spire");
        await storeSearch.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
        await metadata.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        await friends.DidNotReceiveWithAnyArgs().GetFriendsAsync(default);
    }

    [Fact]
    public async Task Subtitle_includes_update_badge_when_state_requires_update()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "G", playtimeMin: 60, stateFlags: 4 | 2)]));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].SubTitle.Should().StartWith("⚠ Update available");
    }

    [Fact]
    public async Task Subtitle_includes_recent_playtime_when_present()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "G", playtimeMin: 450, recent: 120)]));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].SubTitle.Should().Contain("7.5 hrs played");
        results[1].SubTitle.Should().Contain("(2.0 hrs last 2wk)");
    }

    [Fact]
    public async Task Subtitle_omits_recent_playtime_when_zero_or_null()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "G", playtimeMin: 750, recent: null)]));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].SubTitle.Should().NotContain("last 2wk");
    }

    [Fact]
    public async Task Subtitle_says_never_played_when_playtime_is_null_or_zero()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "G", playtimeMin: null)]));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].SubTitle.Should().Contain("Never played");
    }

    [Fact]
    public async Task Result_uses_game_icon_when_available_else_default()
    {
        var withIcon = Game(1, "WithIcon") with { IconPath = @"C:\steam\icon1.jpg" };
        var withoutIcon = Game(2, "NoIcon");
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [withIcon, withoutIcon]));
        var metadata = Substitute.For<IGameMetadataService>();
        metadata.GetAsync(Arg.Any<uint>(), Arg.Any<CancellationToken>()).Returns(GameMetadata.Empty);
        var dispatcher = new QueryDispatcher(
            library,
            Substitute.For<IFuzzyMatcher>(),
            Substitute.For<IStoreSearchService>(),
            Substitute.For<IOwnedGamesService>(),
            Substitute.For<IUserProfileService>(),
            Substitute.For<IApiKeyStore>(),
            new PluginSettings(),
            () => { },
            Substitute.For<IGameIconResolver>(),
            metadata,
            Substitute.For<IFriendsService>(),
            Substitute.For<IAvatarCache>(),
            Substitute.For<IStatusService>(),
            Substitute.For<IAccountSwitcherService>(),
            Substitute.For<IMultiplayerService>(),
            showToast: null,
            changeQuery: null,
            actionKeyword: "st",
            getActiveSteamId: () => null,
            invalidateUserCaches: () => { },
            localPersonaName: null,
            @"C:\steam\default.png",
            NoLog);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].IcoPath.Should().Be(@"C:\steam\icon1.jpg");
        results[2].IcoPath.Should().Be(@"C:\steam\default.png");
    }

    private static Friend Friend(
        ulong id,
        string name,
        PersonaState state = PersonaState.Offline,
        uint? gameId = null,
        string? gameName = null,
        long? lastLogoff = null) => new()
    {
        SteamId64 = id,
        PersonaName = name,
        PersonaState = state,
        CurrentGameAppId = gameId,
        CurrentGameName = gameName,
        LastLogoffUnix = lastLogoff
    };

    [Fact]
    public async Task FriendsList_NotConfigured_ReturnsHint()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = null });

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Contain("Configure");
    }

    [Fact]
    public async Task Empty_query_when_offline_inserts_offline_row_after_launch()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha")]));
        var dispatcher = BuildDispatcher(
            library, Substitute.For<IFuzzyMatcher>(), isNetworkAvailable: () => false);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[0].Title.Should().Be("Launch Steam");
        results.Select(r => r.Title).Should().Contain("You're offline");
    }

    [Fact]
    public async Task FriendsList_WhenOfflineAndEmpty_ShowsOfflineRow()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>());
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends,
            isNetworkAvailable: () => false);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("You're offline");
    }

    [Fact]
    public async Task FriendsList_OrdersInGameFirstThenOnlineThenOffline()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Zeb", PersonaState.Offline),
            Friend(2, "Alex", PersonaState.Online, gameId: 730, gameName: "CS2"),
            Friend(3, "Sam", PersonaState.Online),
            Friend(4, "Bo", PersonaState.Online, gameId: 570, gameName: "Dota 2")
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results.Skip(1).Select(r => r.Title).Should().Equal("Alex", "Bo", "Sam", "Zeb");
    }

    [Fact]
    public async Task FriendsList_FavoritesPinnedToTopUnfiltered()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Zeb", PersonaState.Offline),
            Friend(2, "Alex", PersonaState.Online, gameId: 730, gameName: "CS2")
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var settings = new PluginSettings { SteamId64 = "76561198000000001" };
        settings.FavoriteFriendIds.Add(1UL);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: settings,
            friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results.Skip(1).Select(r => r.Title).Should().Equal("Zeb", "Alex");
    }

    [Fact]
    public async Task FriendsList_WithFilter_FuzzyMatchesAndScoresDescending()
    {
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("al", "Alex").Returns(new MatchResult(true, SearchPrecisionScore.None, [], 80));
        matcher.Match("al", "Sam").Returns(new MatchResult(false, SearchPrecisionScore.None, [], 0));
        matcher.Match("al", "Albert").Returns(new MatchResult(true, SearchPrecisionScore.None, [], 60));
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Alex", PersonaState.Online),
            Friend(2, "Sam", PersonaState.Offline),
            Friend(3, "Albert", PersonaState.Offline)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            matcher,
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.FriendsList("al"),
            CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alex", "Albert");
    }

    [Fact]
    public async Task FriendsList_WithFilter_FavoritesPinnedToTopOverScore()
    {
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("al", "Alex").Returns(new MatchResult(true, SearchPrecisionScore.None, [], 30));
        matcher.Match("al", "Albert").Returns(new MatchResult(true, SearchPrecisionScore.None, [], 90));
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Alex", PersonaState.Online),
            Friend(2, "Albert", PersonaState.Offline)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var settings = new PluginSettings { SteamId64 = "76561198000000001" };
        settings.FavoriteFriendIds.Add(1UL);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            matcher,
            keyStore: keyStore,
            settings: settings,
            friends: friends);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.FriendsList("al"),
            CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alex", "Albert");
    }

    [Fact]
    public async Task FriendsList_WithFilter_NoMatch_ReturnsNoMatchHint()
    {
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new MatchResult(false, SearchPrecisionScore.None, [], 0));
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Alex", PersonaState.Online)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            matcher,
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.FriendsList("xyz"),
            CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Contain("No friend matches");
    }

    [Fact]
    public async Task Empty_query_appends_friends_playing_suffix_when_friend_in_game()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha", playtimeMin: 60)]));
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(100UL, "Alex", PersonaState.Online, gameId: 1U, gameName: "Alpha"),
            Friend(101UL, "Bo",   PersonaState.Online, gameId: 1U, gameName: "Alpha"),
            Friend(102UL, "Sam",  PersonaState.Online, gameId: 999U, gameName: "Other")
        });
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>(), friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].Title.Should().Be("Alpha");
        results[1].SubTitle.Should().EndWith("· 2 friends playing");
    }

    [Fact]
    public async Task Empty_query_omits_friends_playing_suffix_when_friends_fetch_fails()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha", playtimeMin: 60)]));
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Friend>>>(_ => throw new InvalidOperationException("boom"));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>(), friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.Empty(), CancellationToken.None);

        results[1].Title.Should().Be("Alpha");
        results[1].SubTitle.Should().NotContain("playing");
    }

    [Fact]
    public async Task Status_dispatch_returns_three_rows_with_actions()
    {
        var status = Substitute.For<IStatusService>();
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            status: status);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.StatusSwitcher(), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Select(r => r.Title).Should().Equal("Online", "Invisible", "Offline");
        results.Should().AllSatisfy(r => r.Action.Should().NotBeNull());
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_showsCurrentAccountThenSwitchableAccounts()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000001UL, AccountName = "alpha", PersonaName = "Alpha", IsCurrent = true,  LastLoginUnix = 2_000_000_000 },
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 1_000_000_000 }
        });
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher(), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Current: Alpha", "Bravo");
        results[0].SubTitle.Should().Contain("Already active").And.Contain("alpha");
        results[1].SubTitle.Should().Contain("Press Enter to confirm switch").And.Contain("bravo");
        results[0].Preview.Description.Should().Contain("SteamID64: 76561198000000001");
        results[1].Preview.Description.Should().Contain("Available to switch");
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_onlyCurrentAccountSaved_returnsHint()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000001UL, AccountName = "alpha", PersonaName = "Alpha", IsCurrent = true, LastLoginUnix = 2_000_000_000 }
        });
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher(), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Current: Alpha", "No other accounts saved on this machine");
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_listRowAction_NavigatesToConfirm()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 1_000_000_000 }
        });
        var changeQueryCalls = new List<(string Query, bool Requery)>();
        Action<string, bool> changeQuery = (q, r) => changeQueryCalls.Add((q, r));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher,
            changeQuery: changeQuery,
            actionKeyword: "steam");

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher(), CancellationToken.None);

        results.Should().ContainSingle();
        var actionResult = results[0].Action!(new ActionContext());

        actionResult.Should().BeFalse();
        changeQueryCalls.Should().ContainSingle();
        changeQueryCalls[0].Query.Should().Be("steam switch confirm bravo");
        changeQueryCalls[0].Requery.Should().BeTrue();
        _ = switcher.DidNotReceiveWithAnyArgs().SwitchToAsync(default, default);
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_confirm_ShowsConfirmRowForKnownAccount()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 1_000_000_000, AvatarPath = @"C:\steam\bravo.png" }
        });
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher("bravo"), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Title.Should().Be("Confirm: switch to Bravo");
        results[0].SubTitle.Should().Contain("Target account: bravo");
        results[0].SubTitle.Should().Contain("Steam will close and reopen");
        results[0].IcoPath.Should().Be(@"C:\steam\bravo.png");
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_confirm_UnknownAccount_ShowsHint()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 1_000_000_000 }
        });
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher("nonexistent"), CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Be("Account not found");
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_confirm_actionInvokesSwitcher()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 1_000_000_000 }
        });
        switcher.SwitchToAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(SwitchResult.Ok("bravo"));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher("bravo"), CancellationToken.None);
        var actionResult = results[0].Action!(new ActionContext());

        actionResult.Should().BeTrue();
        await Task.Delay(50);
        await switcher.Received(1).SwitchToAsync(76561198000000002UL, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AccountSwitcher_dispatch_usesAvatarPathWhenPresent_elseDefault()
    {
        var switcher = Substitute.For<IAccountSwitcherService>();
        switcher.GetKnownAccounts().Returns(new List<KnownAccount>
        {
            new() { SteamId64 = 76561198000000002UL, AccountName = "bravo", PersonaName = "Bravo", IsCurrent = false, LastLoginUnix = 2_000_000_000, AvatarPath = @"C:\steam\config\avatarcache\76561198000000002.png" },
            new() { SteamId64 = 76561198000000003UL, AccountName = "carol", PersonaName = "Carol", IsCurrent = false, LastLoginUnix = 1_000_000_000, AvatarPath = null }
        });
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            accountSwitcher: switcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.AccountSwitcher(), CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].IcoPath.Should().Be(@"C:\steam\config\avatarcache\76561198000000002.png");
        results[1].IcoPath.Should().Be("icon.png");
    }

    [Fact]
    public async Task MultiplayerWith_emptyName_returns_usage_hint()
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.MultiplayerWith(""), CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Contain("Usage: st multi");
    }

    [Fact]
    public async Task MultiplayerWith_match_friendOverviewIsRowZero_thenGames()
    {
        var games = new SharedMultiplayerGame[]
        {
            new(730u, "CS2", 600, 1200, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-2)),
            new(440u, "TF2", 100, 50,   DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-25))
        };
        var alexFriend = Friend(2UL, "Alex", PersonaState.Online);
        var multi = Substitute.For<IMultiplayerService>();
        multi.FindSharedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MultiplayerResult.Match("Alex", 2UL, games, alexFriend));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            multiplayer: multi);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.MultiplayerWith("alex"), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Select(r => r.Title).Should().Equal("Alex", "CS2", "TF2");
        results[0].SubTitle.Should().Contain("Online");
        results[0].ContextData.Should().BeOfType<ContextData.Friend>();
        results[1].SubTitle.Should().StartWith("You: ").And.NotContain("🟢").And.NotContain("⚫").And.NotContain("🎮");
        results[1].SubTitle.Should().Contain("You: 10h").And.Contain("Alex: 20h");
    }

    [Fact]
    public async Task MultiplayerWith_match_friendInSharedGame_thatGameStillFirstAfterOverview()
    {
        var games = new SharedMultiplayerGame[]
        {
            new(730u, "CS2", 600, 1200, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-2)),
            new(440u, "TF2", 100, 50,   DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-25))
        };
        var alexFriend = Friend(2UL, "Alex", PersonaState.Online, gameId: 730u, gameName: "CS2");
        var multi = Substitute.For<IMultiplayerService>();
        multi.FindSharedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MultiplayerResult.Match("Alex", 2UL, games, alexFriend));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            multiplayer: multi);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.MultiplayerWith("alex"), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alex", "CS2", "TF2");
    }

    [Fact]
    public async Task MultiplayerWith_friendNotFound_returns_no_match_hint()
    {
        var multi = Substitute.For<IMultiplayerService>();
        multi.FindSharedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MultiplayerResult.NoFriendMatch());
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            multiplayer: multi);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.MultiplayerWith("xyz"), CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Contain("No matching friend");
    }

    [Fact]
    public async Task MultiplayerWith_privateProfile_returns_private_or_empty_hint()
    {
        var alexFriend = Friend(2UL, "Alex");
        var multi = Substitute.For<IMultiplayerService>();
        multi.FindSharedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MultiplayerResult.PrivateOrEmpty("Alex", 2UL, alexFriend));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            multiplayer: multi);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.MultiplayerWith("alex"), CancellationToken.None);

        results.Should().ContainSingle()
            .Which.Title.Should().Contain("Alex").And.Contain("private or empty");
    }

    [Fact]
    public async Task VerifyGame_NoFilter_ReturnsLibraryWithVerifyActions()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha", playtimeMin: 60), Game(2, "Bravo", playtimeMin: 30)]));
        var dispatcher = BuildDispatcher(library, Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(new ParsedQuery.VerifyGame(null), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alpha", "Bravo");
        results.Should().AllSatisfy(r =>
        {
            r.SubTitle.Should().StartWith("Verify integrity · ");
            r.Action.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task VerifyGame_Filter_FuzzyMatchesLibrary()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo"), Game(3, "Charlie")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("a", "Alpha").Returns(Hit(80));
        matcher.Match("a", "Bravo").Returns(Hit(40));
        matcher.Match("a", "Charlie").Returns(Miss());
        var dispatcher = BuildDispatcher(library, matcher);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.VerifyGame("a"), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alpha", "Bravo");
        results.Should().AllSatisfy(r => r.SubTitle.Should().StartWith("Verify integrity · "));
    }

    [Fact]
    public async Task FastVerify_returns_verify_rows_without_remote_enrichment()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("a", "Alpha").Returns(Hit(80));
        matcher.Match("a", "Bravo").Returns(Miss());
        var storeSearch = Substitute.For<IStoreSearchService>();
        var metadata = Substitute.For<IGameMetadataService>();
        var friends = Substitute.For<IFriendsService>();
        var dispatcher = BuildDispatcher(
            library,
            matcher,
            storeSearch: storeSearch,
            metadata: metadata,
            friends: friends);

        var results = await dispatcher.BuildFastVerifyResultsAsync("a", CancellationToken.None);

        results.Should().ContainSingle()
            .Which.SubTitle.Should().StartWith("Verify integrity");
        await storeSearch.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
        await metadata.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        await friends.DidNotReceiveWithAnyArgs().GetFriendsAsync(default);
    }

    [Fact]
    public async Task UninstallGame_Filter_FuzzyMatchesLibraryAndLaunchesSteamUninstall()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<InstalledGame>>(
            [Game(1, "Alpha"), Game(2, "Bravo"), Game(3, "Charlie")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match("a", "Alpha").Returns(Hit(80));
        matcher.Match("a", "Bravo").Returns(Hit(40));
        matcher.Match("a", "Charlie").Returns(Miss());
        var dispatcher = BuildDispatcher(
            library,
            matcher,
            actionKeyword: "steam");

        var results = await dispatcher.DispatchAsync(new ParsedQuery.UninstallGame("a"), CancellationToken.None);

        results.Select(r => r.Title).Should().Equal("Alpha", "Bravo");
        results.Should().AllSatisfy(r => r.SubTitle.Should().StartWith("Uninstall · "));
        results[0].SubTitle.Should().Contain("Steam will ask for confirmation");
        var actionResult = results[0].Action!(new ActionContext());
        actionResult.Should().BeTrue();
    }

    [Theory]
    [InlineData(SteamWindow.Settings, "Steam Settings")]
    [InlineData(SteamWindow.Downloads, "Steam Downloads")]
    [InlineData(SteamWindow.BigPicture, "Big Picture Mode")]
    [InlineData(SteamWindow.Screenshots, "Steam Screenshots")]
    [InlineData(SteamWindow.Redeem, "Redeem Steam Key")]
    public async Task OpenSteamWindow_ReturnsSingleActionableRow(SteamWindow window, string expectedTitle)
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>());

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.OpenSteamWindow(window), CancellationToken.None);

        var row = results.Should().ContainSingle().Which;
        row.Title.Should().Be(expectedTitle);
        row.SubTitle.Should().NotBeNullOrWhiteSpace();
        row.Action.Should().NotBeNull();
    }

    [Fact]
    public async Task FriendsList_RowSetsFriendContextData()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(42UL, "Alex", PersonaState.Online, gameId: 730, gameName: "CS2")
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var settings = new PluginSettings { SteamId64 = "76561198000000001" };
        settings.FavoriteFriendIds.Add(42UL);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: settings,
            friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        var ctx = results[1].ContextData.Should().BeOfType<ContextData.Friend>().Which;
        ctx.SteamId64.Should().Be(42UL);
        ctx.PersonaName.Should().Be("Alex");
        ctx.IsFavorite.Should().BeTrue();
        ctx.IsInGame.Should().BeTrue();
        ctx.CurrentGameAppId.Should().Be(730U);
    }

    [Fact]
    public async Task LibraryFilter_ManyMatches_CapsRowsLikeTheFastPass()
    {
        var games = Enumerable.Range(1, 25).Select(i => Game((uint)i, $"Game {i}")).ToList();
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<InstalledGame>>(games));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>()).Returns(Hit(100));
        var dispatcher = BuildDispatcher(library, matcher);

        var enriched = await dispatcher.DispatchAsync(
            new ParsedQuery.LibraryFilter("game"), CancellationToken.None);
        var fast = await dispatcher.BuildFastFilteredResultsAsync("game", CancellationToken.None);

        enriched.Should().HaveCount(fast.Count);
        enriched.Should().HaveCount(10);
    }

    [Fact]
    public async Task LibraryFilter_WhenOfflineWithNoMatches_ShowsOfflineRow()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<InstalledGame>>([Game(1, "Alpha")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>()).Returns(Miss());
        var storeSearch = Substitute.For<IStoreSearchService>();
        storeSearch.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StoreGame>>([]));
        var dispatcher = BuildDispatcher(
            library, matcher, storeSearch: storeSearch, isNetworkAvailable: () => false);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.LibraryFilter("nothing"), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Title.Should().Be("You're offline");
    }

    [Fact]
    public async Task LibraryFilter_WhenOnlineWithNoMatches_StaysEmpty()
    {
        var library = Substitute.For<ILocalLibraryService>();
        library.GetInstalledGamesAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<InstalledGame>>([Game(1, "Alpha")]));
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>()).Returns(Miss());
        var storeSearch = Substitute.For<IStoreSearchService>();
        storeSearch.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StoreGame>>([]));
        var dispatcher = BuildDispatcher(library, matcher, storeSearch: storeSearch);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.LibraryFilter("nothing"), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task BigPicture_WhenRunning_OffersExitInstead()
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            isBigPictureRunning: () => true);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.OpenSteamWindow(SteamWindow.BigPicture), CancellationToken.None);

        var row = results.Should().ContainSingle().Which;
        row.Title.Should().Be("Exit Big Picture Mode");
        row.Action.Should().NotBeNull();
    }

    [Fact]
    public async Task BigPicture_WhenNotRunning_OffersOpen()
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            isBigPictureRunning: () => false);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.OpenSteamWindow(SteamWindow.BigPicture), CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("Big Picture Mode");
    }

    [Fact]
    public async Task BigPicture_WhenDetectionThrows_FallsBackToOpen()
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            isBigPictureRunning: () => throw new InvalidOperationException("enumeration blew up"));

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.OpenSteamWindow(SteamWindow.BigPicture), CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("Big Picture Mode");
    }

    [Fact]
    public async Task OtherSteamWindows_AreUnaffectedByBigPictureState()
    {
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            isBigPictureRunning: () => true);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.OpenSteamWindow(SteamWindow.Settings), CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("Steam Settings");
    }

    [Fact]
    public async Task FriendsList_Unfiltered_PinsOpenFriendsRowFirstWithCounts()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Zeb", PersonaState.Offline),
            Friend(2, "Alex", PersonaState.Online, gameId: 730, gameName: "CS2"),
            Friend(3, "Sam", PersonaState.Online)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results[0].Title.Should().Be("Steam Friends");
        results[0].Score.Should().Be(int.MaxValue);
        results[0].SubTitle.Should().Be("Open the Steam friends list · 1 online · 1 in game");
        results[0].ContextData.Should().BeNull();
    }

    [Fact]
    public async Task FriendsList_Filtered_OmitsOpenFriendsRow()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(2, "Alex", PersonaState.Online)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var matcher = Substitute.For<IFuzzyMatcher>();
        matcher.Match(Arg.Any<string>(), Arg.Any<string>()).Returns(Hit(100));
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            matcher,
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(
            new ParsedQuery.FriendsList("alex"), CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("Alex");
    }

    [Fact]
    public async Task FriendsList_NotConfigured_ShowsOnlyConfigureHint()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Title.Should().NotBe("Steam Friends");
    }

    [Fact]
    public async Task FriendsList_AllFriendsOffline_OmitsCountsFromOpenerSubtitle()
    {
        var friends = Substitute.For<IFriendsService>();
        friends.GetFriendsAsync(Arg.Any<CancellationToken>()).Returns(new List<Friend>
        {
            Friend(1, "Zeb", PersonaState.Offline)
        });
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var dispatcher = BuildDispatcher(
            Substitute.For<ILocalLibraryService>(),
            Substitute.For<IFuzzyMatcher>(),
            keyStore: keyStore,
            settings: new PluginSettings { SteamId64 = "76561198000000001" },
            friends: friends);

        var results = await dispatcher.DispatchAsync(new ParsedQuery.FriendsList(null), CancellationToken.None);

        results[0].SubTitle.Should().Be("Open the Steam friends list");
    }
}
