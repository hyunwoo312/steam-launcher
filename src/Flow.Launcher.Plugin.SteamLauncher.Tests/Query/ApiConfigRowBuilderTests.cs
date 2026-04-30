using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class ApiConfigRowBuilderTests
{
    private static readonly Action<string, string, Exception> NoLog = (_, _, _) => { };

    private static List<Result> BuildShowStatus(
        IApiKeyStore keyStore,
        PluginSettings settings,
        Func<ulong?> getActiveSteamId,
        Action<string, bool>? changeQuery = null,
        string actionKeyword = "st",
        Action? invalidateUserCaches = null,
        Action<string, string>? showToast = null)
    {
        var builder = new ApiConfigRowBuilder(
            keyStore,
            settings,
            saveSettings: () => { },
            invalidateUserCaches ?? (() => { }),
            getActiveSteamId,
            changeQuery,
            showToast,
            actionKeyword,
            defaultIconPath: "icon.png",
            NoLog);

        return builder.Build(new ParsedQuery.ApiConfig(ApiConfigAction.ShowStatus, null));
    }

    [Fact]
    public void StateA_KeyNotConfigured_ReturnsTwoRowsStepOneAndStepTwo()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        var settings = new PluginSettings { SteamId64 = null };

        var rows = BuildShowStatus(keyStore, settings, () => null);

        rows.Should().HaveCount(2);
        rows[0].Title.Should().Be("Step 1 of 2 — Get your Steam Web API key");
        rows[0].SubTitle.Should().Contain("steamcommunity.com/dev/apikey");
        rows[0].Action.Should().NotBeNull();

        rows[1].Title.Should().Be("Step 2 of 2 — Save your API key");
        rows[1].Action.Should().NotBeNull();
    }

    [Fact]
    public void StateA_StepTwoRow_PreFillsSearchBoxOnEnter()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        var settings = new PluginSettings { SteamId64 = null };
        var changeQueryCalls = new List<(string Query, bool Requery)>();
        Action<string, bool> changeQuery = (q, r) => changeQueryCalls.Add((q, r));

        var rows = BuildShowStatus(
            keyStore, settings, () => null,
            changeQuery: changeQuery, actionKeyword: "steam");

        var actionResult = rows[1].Action!(new ActionContext());

        actionResult.Should().BeFalse();
        changeQueryCalls.Should().ContainSingle();
        changeQueryCalls[0].Query.Should().Be("steam api ");
        changeQueryCalls[0].Requery.Should().BeFalse();
    }

    [Fact]
    public void StateB_KeyConfiguredIdMissing_AutoDetected_OffersOneKeypressConfirm()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var settings = new PluginSettings { SteamId64 = null };
        var changeQueryCalls = new List<(string Query, bool Requery)>();
        Action<string, bool> changeQuery = (q, r) => changeQueryCalls.Add((q, r));

        var rows = BuildShowStatus(
            keyStore, settings, () => 76561198000000123UL,
            changeQuery: changeQuery, actionKeyword: "steam");

        rows.Should().HaveCount(2);
        rows[0].Title.Should().Be("Step 2 of 2 — Set your Steam ID");
        rows[0].SubTitle.Should().Contain("Detected as 76561198000000123");
        rows[0].SubTitle.Should().Contain("press Enter to save");
        rows[0].Action.Should().NotBeNull();
        rows[1].Title.Should().Be("Remove API key");

        var actionResult = rows[0].Action!(new ActionContext());

        actionResult.Should().BeFalse();
        changeQueryCalls.Should().ContainSingle();
        changeQueryCalls[0].Query.Should().Be("steam api id 76561198000000123");
        changeQueryCalls[0].Requery.Should().BeTrue();
    }

    [Fact]
    public void StateB_KeyConfiguredIdMissing_NoAutoDetect_ReturnsInstructionalRowWithoutAction()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var settings = new PluginSettings { SteamId64 = null };

        var rows = BuildShowStatus(keyStore, settings, () => null, actionKeyword: "st");

        rows.Should().HaveCount(2);
        rows[0].Title.Should().Be("Step 2 of 2 — Set your Steam ID");
        rows[0].SubTitle.Should().Contain("`st api id <17-digit-steamid64>`");
        rows[0].Action.Should().BeNull();
        rows[1].Title.Should().Be("Remove API key");
    }

    [Fact]
    public void StateC_BothConfigured_ShowsMaskedKeyAndFullSteamIdNoAction()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCD1234567890123456789012345WXYZ");
        var settings = new PluginSettings { SteamId64 = "76561198000000123" };

        var rows = BuildShowStatus(keyStore, settings, () => null, actionKeyword: "st");

        rows.Should().HaveCount(2);
        rows[0].Title.Should().Be("API configured ✓");
        rows[0].SubTitle.Should().Contain("key ABCD…WXYZ");
        rows[0].SubTitle.Should().Contain("ID 76561198000000123");
        rows[0].SubTitle.Should().Contain("`st api <new-key>`");
        rows[0].SubTitle.Should().Contain("`st api id <new-id>`");
        rows[0].Action.Should().BeNull();
        rows[1].Title.Should().Be("Remove API key");
    }

    [Fact]
    public void StateC_ShortKey_FallsBackToFullKey()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABC123");
        var settings = new PluginSettings { SteamId64 = "76561198000000123" };

        var rows = BuildShowStatus(keyStore, settings, () => null);

        rows.Should().HaveCount(2);
        rows[0].SubTitle.Should().Contain("key ABC123");
        rows[0].SubTitle.Should().NotContain("…");
    }

    [Fact]
    public void RemoveKey_StateC_DeletesKeyInvalidatesCachesToastsAndRefreshesQuery()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCD1234567890123456789012345WXYZ");
        var settings = new PluginSettings { SteamId64 = "76561198000000123" };
        var changeQueryCalls = new List<(string Query, bool Requery)>();
        var toastCalls = new List<(string Title, string Body)>();
        var invalidateCount = 0;

        var rows = BuildShowStatus(
            keyStore, settings, () => null,
            changeQuery: (q, r) => changeQueryCalls.Add((q, r)),
            actionKeyword: "st",
            invalidateUserCaches: () => invalidateCount++,
            showToast: (t, b) => toastCalls.Add((t, b)));

        rows[1].Title.Should().Be("Remove API key");
        var actionResult = rows[1].Action!(new ActionContext());

        actionResult.Should().BeFalse();
        keyStore.Received(1).Save(null);
        invalidateCount.Should().Be(1);
        toastCalls.Should().ContainSingle().Which.Title.Should().Be("API key removed");
        changeQueryCalls.Should().ContainSingle();
        changeQueryCalls[0].Query.Should().Be("st api");
        changeQueryCalls[0].Requery.Should().BeTrue();
    }

    [Fact]
    public void SaveKey_ValidKey_ReturnsSaveRowAndPersistsOnAction()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        var settings = new PluginSettings();

        var changeQueryCalls = new List<(string Query, bool Requery)>();
        var toastCalls = new List<(string Title, string Body)>();

        var builder = new ApiConfigRowBuilder(
            keyStore, settings, saveSettings: () => { },
            invalidateUserCaches: () => { },
            getActiveSteamId: () => null,
            changeQuery: (q, r) => changeQueryCalls.Add((q, r)),
            showToast: (t, b) => toastCalls.Add((t, b)),
            actionKeyword: "st", defaultIconPath: "icon.png", NoLog);

        var rows = builder.Build(new ParsedQuery.ApiConfig(ApiConfigAction.SaveKey, "MYKEY1234567890ABCDEF1234567890XY"));

        rows.Should().ContainSingle();
        rows[0].Title.Should().Be("Save API key");
        rows[0].Action!(new ActionContext()).Should().BeFalse();
        keyStore.Received(1).Save("MYKEY1234567890ABCDEF1234567890XY");
        toastCalls.Should().ContainSingle().Which.Title.Should().Be("API key saved");
        changeQueryCalls.Should().ContainSingle().Which.Query.Should().Be("st api");
    }

    [Fact]
    public void SaveSteamId_ValidId_ReturnsSaveRowAndPersistsOnAction()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        var settings = new PluginSettings();
        var saveCount = 0;

        var changeQueryCalls = new List<(string Query, bool Requery)>();
        var toastCalls = new List<(string Title, string Body)>();

        var builder = new ApiConfigRowBuilder(
            keyStore, settings, saveSettings: () => saveCount++,
            invalidateUserCaches: () => { },
            getActiveSteamId: () => null,
            changeQuery: (q, r) => changeQueryCalls.Add((q, r)),
            showToast: (t, b) => toastCalls.Add((t, b)),
            actionKeyword: "st", defaultIconPath: "icon.png", NoLog);

        var rows = builder.Build(new ParsedQuery.ApiConfig(ApiConfigAction.SaveSteamId, "76561198000000123"));

        rows.Should().ContainSingle();
        rows[0].Action!(new ActionContext()).Should().BeFalse();
        settings.SteamId64.Should().Be("76561198000000123");
        saveCount.Should().Be(1);
        toastCalls.Should().ContainSingle().Which.Title.Should().Be("Steam ID saved");
        changeQueryCalls.Should().ContainSingle().Which.Query.Should().Be("st api");
    }
}
