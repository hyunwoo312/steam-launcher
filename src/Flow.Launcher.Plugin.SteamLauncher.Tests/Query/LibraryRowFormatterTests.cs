using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class LibraryRowFormatterTests
{
    private static InstalledGame Game(long? playtimeMin = 60) => new()
    {
        AppId = 730,
        Name = "CS2",
        InstallDir = "CS2",
        LibraryPath = @"C:\steam",
        SizeOnDiskBytes = 35_000_000_000,
        StateFlags = 4,
        PlaytimeMinutes = playtimeMin
    };

    [Fact]
    public void Subtitle_NoFriendsPlaying_OmitsSuffix()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty);

        subtitle.Should().NotContain("playing");
        subtitle.Should().NotContain("friend");
    }

    [Fact]
    public void Subtitle_OneFriendPlaying_AppendsSingularSuffix()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty, friendsPlaying: 1);

        subtitle.Should().EndWith("· 1 friend playing");
    }

    [Fact]
    public void Subtitle_MultipleFriendsPlaying_AppendsPluralSuffix()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty, friendsPlaying: 3);

        subtitle.Should().EndWith("· 3 friends playing");
    }

    [Fact]
    public void Subtitle_ZeroFriendsPlaying_OmitsSuffix()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty, friendsPlaying: 0);

        subtitle.Should().NotContain("playing");
    }
}
