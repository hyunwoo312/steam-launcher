using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class FriendRowFormatterTests
{
    private static Friend Make(
        PersonaState state = PersonaState.Offline,
        uint? appId = null,
        string? gameName = null,
        long? lastLogoff = null) => new()
    {
        SteamId64 = 76561198000000002UL,
        PersonaName = "Alex",
        PersonaState = state,
        CurrentGameAppId = appId,
        CurrentGameName = gameName,
        LastLogoffUnix = lastLogoff
    };

    [Fact]
    public void Subtitle_InGame_FormatsWithGamepadAndName()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online, 730u, "CS2"));

        subtitle.Should().Be("🎮 Playing CS2");
    }

    [Fact]
    public void Subtitle_InGame_NullName_FallsBackToAppId()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online, 730u, null));

        subtitle.Should().Be("🎮 Playing AppID 730");
    }

    [Fact]
    public void Subtitle_Online_NoGame_GreenDot()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online));

        subtitle.Should().Be("🟢 Online");
    }

    [Fact]
    public void Subtitle_Away_YellowDot()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Away));

        subtitle.Should().Be("🟡 Away");
    }

    [Fact]
    public void Subtitle_Busy_RedDot()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Busy));

        subtitle.Should().Be("🔴 Busy");
    }

    [Fact]
    public void Subtitle_Snooze_LabelledSnooze()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Snooze));

        subtitle.Should().Be("🟡 Snooze");
    }

    [Fact]
    public void Subtitle_LookingToPlay_HasGreenDotAndLabel()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.LookingToPlay));

        subtitle.Should().Be("🟢 Looking to play");
    }

    [Fact]
    public void Subtitle_Offline_WithLastLogoff_IncludesRelativeAgo()
    {
        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Offline, lastLogoff: twoDaysAgo));

        subtitle.Should().StartWith("⚫ Offline · last seen ");
        subtitle.Should().Contain("days ago");
    }

    [Fact]
    public void Subtitle_Offline_NoLastLogoff_PlainOffline()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Offline));

        subtitle.Should().Be("⚫ Offline");
    }

    [Fact]
    public void Subtitle_OnlineButNotInGame_GreenDotEvenWhenGameNameNull()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online, appId: null, gameName: "Spotify"));

        subtitle.Should().Be("🟢 Online");
    }

    [Fact]
    public void Subtitle_Favorite_PrefixesStarOnInGame()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online, 730u, "CS2"), isFavorite: true);

        subtitle.Should().Be("⭐️ 🎮 Playing CS2");
    }

    [Fact]
    public void Subtitle_Favorite_PrefixesStarOnOffline()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Offline), isFavorite: true);

        subtitle.Should().Be("⭐️ ⚫ Offline");
    }

    [Fact]
    public void Subtitle_NotFavorite_NoStarPrefix()
    {
        var subtitle = FriendRowFormatter.BuildSubtitle(Make(PersonaState.Online), isFavorite: false);

        subtitle.Should().Be("🟢 Online");
    }
}
