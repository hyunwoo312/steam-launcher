using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class ContextMenuBuilderTests
{
    private static readonly Action<string, string, Exception> NoLog = (_, _, _) => { };

    private static ContextMenuBuilder Build(
        PluginSettings? settings = null,
        Action? saveSettings = null,
        IPublicAPI? api = null) =>
        new(api ?? Substitute.For<IPublicAPI>(), settings ?? new PluginSettings(), saveSettings ?? (() => { }), "icon.png", NoLog);

    [Fact]
    public void Build_InstalledGame_IncludesOpenFolderRow()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", @"C:\Steam\steamapps\common\CS2"));

        rows.Should().Contain(r => r.Title == "Open game folder");
    }

    [Fact]
    public void Build_StoreSearchGame_OmitsOpenFolderRow()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", InstallPath: null));

        rows.Should().NotContain(r => r.Title == "Open game folder");
        rows.Should().NotContain(r => r.Title == "Uninstall");
    }

    [Fact]
    public void Build_GameMenu_FirstRowIsLaunchGame()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", null));

        rows[0].Title.Should().Be("Launch game");
        rows[0].SubTitle.Should().Be("Launch via Steam");
    }

    [Fact]
    public void Build_GameMenu_DoesNotIncludeSteamDb()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", null));

        rows.Should().NotContain(r => r.Title == "Open SteamDB");
    }

    [Fact]
    public void Build_GameMenu_OrderMatchesSpec()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", @"C:\foo"));

        rows.Select(r => r.Title).Should().Equal(
            "Launch game",
            "Open store page",
            "Open library page",
            "Open community guides",
            "Open community discussions",
            "Open game properties",
            "Uninstall",
            "Open game folder");
    }

    [Fact]
    public void Build_InstalledGame_IncludesUninstallRow()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", @"C:\foo"));

        rows.Should().Contain(r => r.Title == "Uninstall"
            && r.SubTitle == "Open Steam's uninstall confirmation");
    }

    [Fact]
    public void Build_AllRowsHaveGlyphSetForIconColumn()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Game(730, "CS2", @"C:\foo"));

        rows.Should().AllSatisfy(r =>
        {
            r.Glyph.Should().NotBeNull("each row uses Glyph for the left icon column");
            r.Glyph!.Glyph.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public void Build_FriendMenu_FiveRowsInSpecifiedOrder()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: true, CurrentGameAppId: 730U));

        rows.Should().HaveCount(5);
        rows.Select(r => r.Title).Should().Equal("DM", "Invite to game", "Join game", "Open profile", "Favorite");
    }

    [Fact]
    public void Build_FriendMenu_NotInGame_JoinSubtitleSaysNotJoinable()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: false, CurrentGameAppId: null));

        var join = rows.Single(r => r.Title == "Join game");
        join.SubTitle.Should().Be("Friend is not in a joinable game");
    }

    [Fact]
    public void Build_FriendMenu_InGame_JoinSubtitleSaysJoinLobby()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: true, CurrentGameAppId: 730U));

        var join = rows.Single(r => r.Title == "Join game");
        join.SubTitle.Should().Be("Join their lobby");
    }

    [Fact]
    public void Build_FriendMenu_NotFavorite_LastRowSaysFavorite()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: false, CurrentGameAppId: null));

        rows[^1].Title.Should().Be("Favorite");
        rows[^1].SubTitle.Should().Be("Pin to top of st friends results");
    }

    [Fact]
    public void Build_FriendMenu_AlreadyFavorite_LastRowSaysUnfavorite()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: true, IsInGame: false, CurrentGameAppId: null));

        rows[^1].Title.Should().Be("Unfavorite");
        rows[^1].SubTitle.Should().Be("Remove from priority list");
    }

    [Fact]
    public void Build_FriendMenu_FavoriteToggleAddsToSettingsAndCallsSave()
    {
        var settings = new PluginSettings();
        var saveCalls = 0;
        var builder = Build(settings, () => saveCalls++);

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: false, CurrentGameAppId: null));
        var favRow = rows.Single(r => r.Title == "Favorite");

        favRow.Action!(new ActionContext());

        settings.FavoriteFriendIds.Should().Contain(42UL);
        saveCalls.Should().Be(1);
    }

    [Fact]
    public void Build_FriendMenu_UnfavoriteToggleRemovesFromSettingsAndCallsSave()
    {
        var settings = new PluginSettings();
        settings.FavoriteFriendIds.Add(42UL);
        var saveCalls = 0;
        var builder = Build(settings, () => saveCalls++);

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: true, IsInGame: false, CurrentGameAppId: null));
        var unfavRow = rows.Single(r => r.Title == "Unfavorite");

        unfavRow.Action!(new ActionContext());

        settings.FavoriteFriendIds.Should().NotContain(42UL);
        saveCalls.Should().Be(1);
    }

    [Fact]
    public void Build_FriendMenu_FavoriteToggleShowsToastAndRefreshesInPlace()
    {
        var api = Substitute.For<IPublicAPI>();
        var builder = Build(api: api);

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: false, CurrentGameAppId: null));
        var favRow = rows.Single(r => r.Title == "Favorite");

        var result = favRow.Action!(new ActionContext());

        result.Should().BeFalse();
        api.Received(1).ShowMsg("Favorited Alex", "1 favorite total", Arg.Any<string>());
        api.Received(1).ReQuery(true);
    }

    [Fact]
    public void Build_FriendMenu_UnfavoriteToggleShowsToastAndRefreshesInPlace()
    {
        var api = Substitute.For<IPublicAPI>();
        var settings = new PluginSettings();
        settings.FavoriteFriendIds.Add(42UL);
        var builder = Build(settings, api: api);

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: true, IsInGame: false, CurrentGameAppId: null));
        var unfavRow = rows.Single(r => r.Title == "Unfavorite");

        var result = unfavRow.Action!(new ActionContext());

        result.Should().BeFalse();
        api.Received(1).ShowMsg("Unfavorited Alex", "0 favorites total", Arg.Any<string>());
        api.Received(1).ReQuery(true);
    }

    [Fact]
    public void Build_FriendMenu_AllRowsHaveGlyph()
    {
        var builder = Build();

        var rows = builder.Build(new ContextData.Friend(42UL, "Alex", IsFavorite: false, IsInGame: true, CurrentGameAppId: 730U));

        rows.Should().AllSatisfy(r =>
        {
            r.Glyph.Should().NotBeNull();
            r.Glyph!.Glyph.Should().NotBeNullOrEmpty();
        });
    }
}
