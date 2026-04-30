using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class MultiplayerRowFormatterTests
{
    private static SharedMultiplayerGame Make(
        uint appId = 730,
        long myMin = 0,
        long friendMin = 0,
        DateTimeOffset? myLastPlayed = null,
        DateTimeOffset? friendLastPlayed = null) => new(
            AppId: appId,
            Name: "CS2",
            MyPlaytimeMinutes: myMin,
            FriendPlaytimeMinutes: friendMin,
            MyLastPlayed: myLastPlayed,
            FriendLastPlayed: friendLastPlayed);

    [Fact]
    public void BuildSubtitle_BothPlayedRecently_ShowsCompactPlayedRelative()
    {
        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2);
        var subtitle = MultiplayerRowFormatter.BuildSubtitle(
            "Alex",
            Make(myMin: 600, friendMin: 1200, myLastPlayed: twoDaysAgo, friendLastPlayed: twoDaysAgo.AddDays(-5)));

        subtitle.Should().Contain("You: 10h").And.Contain("Alex: 20h");
        subtitle.Should().Contain("played 1w ago");
    }

    [Fact]
    public void BuildSubtitle_NeitherPlayed_OmitsPlayedSegment()
    {
        var subtitle = MultiplayerRowFormatter.BuildSubtitle("Alex", Make());

        subtitle.Should().Be("You: 0h · Alex: 0h");
    }

    [Fact]
    public void BuildSubtitle_OnlyOneSideHasPlayed_OmitsPlayedSegment()
    {
        var subtitle = MultiplayerRowFormatter.BuildSubtitle(
            "Alex",
            Make(myMin: 60, myLastPlayed: DateTimeOffset.UtcNow.AddDays(-1)));

        subtitle.Should().Be("You: 1h · Alex: 0h");
    }

    [Fact]
    public void BuildSubtitle_PlayedIsTheOlderOfTheTwo()
    {
        var subtitle = MultiplayerRowFormatter.BuildSubtitle(
            "Alex",
            Make(
                myMin: 60, friendMin: 60,
                myLastPlayed: DateTimeOffset.UtcNow.AddDays(-1),
                friendLastPlayed: DateTimeOffset.UtcNow.AddDays(-50)));

        subtitle.Should().Contain("played 1mo ago");
    }

    [Fact]
    public void BuildSubtitle_OmitsAnyStatusOrEmojiPrefix()
    {
        var subtitle = MultiplayerRowFormatter.BuildSubtitle(
            "Alex", Make(myMin: 60, friendMin: 60));

        subtitle.Should().NotContain("🟢").And.NotContain("⚫").And.NotContain("🎮");
        subtitle.Should().Be("You: 1h · Alex: 1h");
    }

    [Fact]
    public void BuildSubtitle_HoursUnder60Minutes_ShowsMinutes()
    {
        var subtitle = MultiplayerRowFormatter.BuildSubtitle(
            "Alex", Make(myMin: 30, friendMin: 45));

        subtitle.Should().Contain("You: 30m").And.Contain("Alex: 45m");
    }
}
