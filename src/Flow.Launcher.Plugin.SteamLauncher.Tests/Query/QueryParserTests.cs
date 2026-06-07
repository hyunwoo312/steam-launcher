using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class QueryParserTests
{
    [Fact]
    public void Parse_Null_ReturnsEmpty()
    {
        var result = QueryParser.Parse(null);

        result.Should().BeOfType<ParsedQuery.Empty>();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var result = QueryParser.Parse("");

        result.Should().BeOfType<ParsedQuery.Empty>();
    }

    [Fact]
    public void Parse_Whitespace_ReturnsEmpty()
    {
        var result = QueryParser.Parse("   ");

        result.Should().BeOfType<ParsedQuery.Empty>();
    }

    [Fact]
    public void Parse_NonEmpty_ReturnsLibraryFilterWithTrimmedTerm()
    {
        var result = QueryParser.Parse("  half-life  ");

        result.Should().BeOfType<ParsedQuery.LibraryFilter>()
            .Which.Term.Should().Be("half-life");
    }

    [Fact]
    public void Parse_OverlongInput_TruncatesToMax()
    {
        var input = new string('a', 500);

        var result = QueryParser.Parse(input);

        result.Should().BeOfType<ParsedQuery.LibraryFilter>()
            .Which.Term.Length.Should().Be(200);
    }

    [Fact]
    public void Parse_ApiKeyword_NoArg_ReturnsApiConfigShowStatus()
    {
        var result = QueryParser.Parse("api");

        result.Should().BeOfType<ParsedQuery.ApiConfig>()
            .Which.Action.Should().Be(ApiConfigAction.ShowStatus);
    }

    [Fact]
    public void Parse_ApiKeywordWithLongHexString_ReturnsSaveKey()
    {
        var key = new string('A', 32);

        var result = QueryParser.Parse($"api {key}");

        var config = result.Should().BeOfType<ParsedQuery.ApiConfig>().Which;
        config.Action.Should().Be(ApiConfigAction.SaveKey);
        config.Argument.Should().Be(key);
    }

    [Fact]
    public void Parse_ApiKeywordWithIdSubcommand_ReturnsSaveSteamId()
    {
        var result = QueryParser.Parse("api id 76561198000000001");

        var config = result.Should().BeOfType<ParsedQuery.ApiConfig>().Which;
        config.Action.Should().Be(ApiConfigAction.SaveSteamId);
        config.Argument.Should().Be("76561198000000001");
    }

    [Fact]
    public void Parse_MeKeyword_ReturnsMe()
    {
        var result = QueryParser.Parse("me");

        result.Should().BeOfType<ParsedQuery.Me>();
    }

    [Fact]
    public void Parse_NewKeyword_ReturnsRecentlyAddedNeverPlayed()
    {
        var result = QueryParser.Parse("new");

        result.Should().BeOfType<ParsedQuery.RecentlyAddedNeverPlayed>();
    }

    [Fact]
    public void Parse_KeywordWithExtraText_StillRoutesToKeyword()
    {
        var result = QueryParser.Parse("me extra");

        result.Should().BeOfType<ParsedQuery.LibraryFilter>()
            .Which.Term.Should().Be("me extra");
    }

    [Fact]
    public void Parse_friends_NoArg_ReturnsFriendsListWithNullFilter()
    {
        var result = QueryParser.Parse("friends");

        var fl = result.Should().BeOfType<ParsedQuery.FriendsList>().Which;
        fl.Filter.Should().BeNull();
    }

    [Fact]
    public void Parse_friends_WithTerm_ReturnsFriendsListWithTerm()
    {
        var result = QueryParser.Parse("friends alex");

        var fl = result.Should().BeOfType<ParsedQuery.FriendsList>().Which;
        fl.Filter.Should().Be("alex");
    }

    [Fact]
    public void Parse_friends_WithMixedCaseAndPadding_TrimsTerm()
    {
        var result = QueryParser.Parse("FRIENDS   alex  ");

        var fl = result.Should().BeOfType<ParsedQuery.FriendsList>().Which;
        fl.Filter.Should().Be("alex");
    }

    [Fact]
    public void Parse_status_ReturnsStatusSwitcher()
    {
        var result = QueryParser.Parse("status");

        result.Should().BeOfType<ParsedQuery.StatusSwitcher>();
    }

    [Fact]
    public void Parse_status_MixedCase_ReturnsStatusSwitcher()
    {
        var result = QueryParser.Parse("STATUS");

        result.Should().BeOfType<ParsedQuery.StatusSwitcher>();
    }

    [Fact]
    public void Parse_switch_ReturnsAccountSwitcher()
    {
        var result = QueryParser.Parse("switch");

        var acs = result.Should().BeOfType<ParsedQuery.AccountSwitcher>().Which;
        acs.ConfirmAccountName.Should().BeNull();
    }

    [Fact]
    public void Parse_switch_confirm_WithAccountName_ReturnsAccountSwitcherWithName()
    {
        var result = QueryParser.Parse("switch confirm bravo");

        var acs = result.Should().BeOfType<ParsedQuery.AccountSwitcher>().Which;
        acs.ConfirmAccountName.Should().Be("bravo");
    }

    [Fact]
    public void Parse_switch_confirm_WithSpacesInAccountName_PreservesNameVerbatim()
    {
        var result = QueryParser.Parse("switch confirm Cool Account 1");

        var acs = result.Should().BeOfType<ParsedQuery.AccountSwitcher>().Which;
        acs.ConfirmAccountName.Should().Be("Cool Account 1");
    }

    [Fact]
    public void Parse_multi_NoArg_ReturnsMultiplayerWithEmptyName()
    {
        var result = QueryParser.Parse("multi");

        var mw = result.Should().BeOfType<ParsedQuery.MultiplayerWith>().Which;
        mw.FriendName.Should().BeEmpty();
    }

    [Fact]
    public void Parse_multi_WithName_ReturnsMultiplayerWithName()
    {
        var result = QueryParser.Parse("multi alex");

        var mw = result.Should().BeOfType<ParsedQuery.MultiplayerWith>().Which;
        mw.FriendName.Should().Be("alex");
    }

    [Fact]
    public void Parse_multi_WithMixedCaseAndPadding_TrimsName()
    {
        var result = QueryParser.Parse("MULTI   Alex Bob  ");

        var mw = result.Should().BeOfType<ParsedQuery.MultiplayerWith>().Which;
        mw.FriendName.Should().Be("Alex Bob");
    }

    [Fact]
    public void Parse_verify_NoArg_ReturnsVerifyGameWithNullFilter()
    {
        var result = QueryParser.Parse("verify");

        var v = result.Should().BeOfType<ParsedQuery.VerifyGame>().Which;
        v.Filter.Should().BeNull();
    }

    [Fact]
    public void Parse_verify_WithTerm_ReturnsVerifyGameWithTerm()
    {
        var result = QueryParser.Parse("verify half-life");

        var v = result.Should().BeOfType<ParsedQuery.VerifyGame>().Which;
        v.Filter.Should().Be("half-life");
    }

    [Fact]
    public void Parse_verify_WithMixedCaseAndPadding_TrimsTerm()
    {
        var result = QueryParser.Parse("VERIFY   portal  ");

        var v = result.Should().BeOfType<ParsedQuery.VerifyGame>().Which;
        v.Filter.Should().Be("portal");
    }

    [Fact]
    public void Parse_uninstall_NoArg_ReturnsUninstallGameWithNullFilter()
    {
        var result = QueryParser.Parse("uninstall");

        var u = result.Should().BeOfType<ParsedQuery.UninstallGame>().Which;
        u.Filter.Should().BeNull();
    }

    [Fact]
    public void Parse_uninstall_WithTerm_ReturnsUninstallGameWithTerm()
    {
        var result = QueryParser.Parse("uninstall half-life");

        var u = result.Should().BeOfType<ParsedQuery.UninstallGame>().Which;
        u.Filter.Should().Be("half-life");
    }

    [Fact]
    public void Parse_settings_ReturnsOpenSteamSettings()
    {
        var result = QueryParser.Parse("settings");

        result.Should().BeOfType<ParsedQuery.OpenSteamSettings>();
    }

    [Fact]
    public void Parse_settings_MixedCase_ReturnsOpenSteamSettings()
    {
        var result = QueryParser.Parse("SETTINGS");

        result.Should().BeOfType<ParsedQuery.OpenSteamSettings>();
    }

    [Fact]
    public void Parse_downloads_ReturnsOpenSteamDownloads()
    {
        var result = QueryParser.Parse("downloads");

        result.Should().BeOfType<ParsedQuery.OpenSteamDownloads>();
    }

    [Fact]
    public void Parse_downloads_MixedCase_ReturnsOpenSteamDownloads()
    {
        var result = QueryParser.Parse("Downloads");

        result.Should().BeOfType<ParsedQuery.OpenSteamDownloads>();
    }
}
