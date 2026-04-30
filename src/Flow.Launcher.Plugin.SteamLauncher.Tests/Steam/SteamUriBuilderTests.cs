using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Steam;

public sealed class SteamUriBuilderTests
{
    [Fact]
    public void RunGame_UsesRungameidScheme()
    {
        SteamUriBuilder.RunGame(730).Should().Be("steam://rungameid/730");
    }

    [Fact]
    public void Install_BuildsCorrectUri()
    {
        SteamUriBuilder.Install(730).Should().Be("steam://install/730");
    }

    [Fact]
    public void Uninstall_BuildsCorrectUri()
    {
        SteamUriBuilder.Uninstall(730).Should().Be("steam://uninstall/730");
    }

    [Fact]
    public void Store_BuildsCorrectUri()
    {
        SteamUriBuilder.Store(730).Should().Be("steam://store/730");
    }

    [Fact]
    public void LibraryDetails_BuildsNavUri()
    {
        SteamUriBuilder.LibraryDetails(730).Should().Be("steam://nav/games/details/730");
    }

    [Fact]
    public void GameProperties_BuildsCorrectUri()
    {
        SteamUriBuilder.GameProperties(730).Should().Be("steam://gameproperties/730");
    }

    [Fact]
    public void FriendsMessage_AcceptsSteamId64()
    {
        SteamUriBuilder.FriendsMessage(76561198000000001UL)
            .Should().Be("steam://friends/message/76561198000000001");
    }

    [Fact]
    public void FriendsJoinGame_BuildsCorrectUri()
    {
        SteamUriBuilder.FriendsJoinGame(76561198000000001UL)
            .Should().Be("steam://friends/joingame/76561198000000001");
    }

    [Fact]
    public void Profile_UsesSteamIdPagePath()
    {
        SteamUriBuilder.Profile(76561198000000001UL)
            .Should().Be("steam://url/SteamIDPage/76561198000000001");
    }

    [Fact]
    public void Validate_BuildsCorrectUri()
    {
        SteamUriBuilder.Validate(730).Should().Be("steam://validate/730");
    }

    [Fact]
    public void OpenSettings_ReturnsExpectedUri()
    {
        SteamUriBuilder.OpenSettings().Should().Be("steam://open/settings");
    }

    [Fact]
    public void OpenDownloads_ReturnsExpectedUri()
    {
        SteamUriBuilder.OpenDownloads().Should().Be("steam://open/downloads");
    }
}
