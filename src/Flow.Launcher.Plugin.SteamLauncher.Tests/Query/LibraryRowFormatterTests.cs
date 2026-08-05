using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class LibraryRowFormatterTests
{
    private static InstalledGame Game(long? playtimeMin = 60, uint stateFlags = 4) => new()
    {
        AppId = 730,
        Name = "CS2",
        InstallDir = "CS2",
        LibraryPath = @"C:\steam",
        SizeOnDiskBytes = 35_000_000_000,
        StateFlags = stateFlags,
        PlaytimeMinutes = playtimeMin
    };

    [Fact]
    public void Subtitle_Uninstalling_LeadsWithUninstallingBadge()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(stateFlags: 4 | 2048), GameMetadata.Empty);

        subtitle.Should().StartWith("🗑 Uninstalling");
    }

    [Fact]
    public void Subtitle_FilesMissing_LeadsWithRepairBadge()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(stateFlags: 4 | 32), GameMetadata.Empty);

        subtitle.Should().StartWith("⚠ Files missing");
    }

    [Fact]
    public void Subtitle_FullyInstalled_HasNoBadge()
    {
        var subtitle = LibraryRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty);

        subtitle.Should().NotContain("⚠").And.NotContain("🗑").And.NotContain("⬇");
    }

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
