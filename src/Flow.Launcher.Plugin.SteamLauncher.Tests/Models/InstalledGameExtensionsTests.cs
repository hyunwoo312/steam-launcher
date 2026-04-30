using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Models;

public sealed class InstalledGameExtensionsTests
{
    private static InstalledGame Game(uint flags) => new()
    {
        AppId = 1,
        Name = "Test",
        InstallDir = "Test",
        LibraryPath = @"C:\steam",
        StateFlags = flags
    };

    [Fact]
    public void GetInstallState_FullyInstalled_ReturnsInstalled()
    {
        Game(4).GetInstallState().Should().Be(InstallState.Installed);
    }

    [Fact]
    public void GetInstallState_UpdateRequiredOnly_ReturnsUpdateRequired()
    {
        Game(4 | 2).GetInstallState().Should().Be(InstallState.UpdateRequired);
    }

    [Fact]
    public void GetInstallState_UpdateRunning_ReturnsUpdating()
    {
        Game(4 | 256).GetInstallState().Should().Be(InstallState.Updating);
    }

    [Fact]
    public void GetInstallState_UpdateStarted_ReturnsUpdating()
    {
        Game(4 | 1024).GetInstallState().Should().Be(InstallState.Updating);
    }

    [Fact]
    public void GetInstallState_UpdatePaused_ReturnsUpdatePaused()
    {
        Game(4 | 512).GetInstallState().Should().Be(InstallState.UpdatePaused);
    }

    [Fact]
    public void GetInstallState_RunningTrumpsRequired_ReturnsUpdating()
    {
        Game(4 | 2 | 256).GetInstallState().Should().Be(InstallState.Updating);
    }

    [Fact]
    public void GetInstallState_PausedTrumpsRequired_ReturnsUpdatePaused()
    {
        Game(4 | 2 | 512).GetInstallState().Should().Be(InstallState.UpdatePaused);
    }

    [Fact]
    public void GetInstallState_ZeroFlags_ReturnsInstalled()
    {
        Game(0).GetInstallState().Should().Be(InstallState.Installed);
    }
}
