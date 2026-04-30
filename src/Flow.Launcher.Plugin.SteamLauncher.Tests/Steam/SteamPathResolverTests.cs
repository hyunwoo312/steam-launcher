using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Steam;

public sealed class SteamPathResolverTests
{
    [Fact]
    public void GetSteamInstallPath_RegistryHasValue_ReturnsIt()
    {
        var registry = Substitute.For<IRegistryReader>();
        registry.ReadCurrentUserString(@"Software\Valve\Steam", "SteamPath")
            .Returns(@"C:/Program Files (x86)/Steam");
        var parser = Substitute.For<IVdfParser>();
        var resolver = new SteamPathResolver(registry, parser);

        var result = resolver.GetSteamInstallPath();

        result.Should().Be(@"C:\Program Files (x86)\Steam");
    }

    [Fact]
    public void GetSteamInstallPath_RegistryEmpty_Throws()
    {
        var registry = Substitute.For<IRegistryReader>();
        registry.ReadCurrentUserString(default!, default!)
            .ReturnsForAnyArgs((string?)null);
        var parser = Substitute.For<IVdfParser>();
        var resolver = new SteamPathResolver(registry, parser);

        var act = () => resolver.GetSteamInstallPath();

        act.Should().Throw<SteamPathNotFoundException>();
    }

    [Fact]
    public void GetActiveSteamId64_PicksUserWithMostRecentEqualsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "flow-steam-loginusers-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(tempDir, "config"));
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "config", "loginusers.vdf"),
                """
                "users"
                {
                    "76561198000000001"
                    {
                        "AccountName"  "alpha"
                        "MostRecent"   "0"
                    }
                    "76561198000000002"
                    {
                        "AccountName"  "bravo"
                        "MostRecent"   "1"
                    }
                }
                """);
            var registry = Substitute.For<IRegistryReader>();
            registry.ReadCurrentUserString(@"Software\Valve\Steam", "SteamPath").Returns(tempDir.Replace('\\', '/'));
            var resolver = new SteamPathResolver(registry, new VdfParser());

            var id = resolver.GetActiveSteamId64();

            id.Should().Be(76561198000000002UL);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetActiveSteamId64_NoMostRecentFlag_FallsBackToFirstUser()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "flow-steam-loginusers-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(tempDir, "config"));
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "config", "loginusers.vdf"),
                """
                "users"
                {
                    "76561198000000003"
                    {
                        "AccountName"  "only"
                    }
                }
                """);
            var registry = Substitute.For<IRegistryReader>();
            registry.ReadCurrentUserString(default!, default!).ReturnsForAnyArgs(tempDir.Replace('\\', '/'));
            var resolver = new SteamPathResolver(registry, new VdfParser());

            resolver.GetActiveSteamId64().Should().Be(76561198000000003UL);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetActiveSteamId64_NoLoginUsersFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "flow-steam-loginusers-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var registry = Substitute.For<IRegistryReader>();
            registry.ReadCurrentUserString(default!, default!).ReturnsForAnyArgs(tempDir.Replace('\\', '/'));
            var resolver = new SteamPathResolver(registry, new VdfParser());

            resolver.GetActiveSteamId64().Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetLibraryPaths_ParsesLibraryFoldersVdf_ReturnsAllPaths()
    {
        // Resolver short-circuits to [steamRoot] if libraryfolders.vdf doesn't exist
        // on disk, so we lay out a real temp Steam tree instead of substituting the parser.
        var tempDir = Path.Combine(Path.GetTempPath(), "flow-steam-libraryfolders-" + Guid.NewGuid());
        var steamappsDir = Path.Combine(tempDir, "steamapps");
        Directory.CreateDirectory(steamappsDir);
        try
        {
            var fixtureSrc = Path.Combine(AppContext.BaseDirectory, "Fixtures", "libraryfolders.vdf");
            File.Copy(fixtureSrc, Path.Combine(steamappsDir, "libraryfolders.vdf"));

            var registry = Substitute.For<IRegistryReader>();
            registry.ReadCurrentUserString(@"Software\Valve\Steam", "SteamPath").Returns(tempDir.Replace('\\', '/'));
            var resolver = new SteamPathResolver(registry, new VdfParser());

            var paths = resolver.GetLibraryPaths();

            paths.Should().BeEquivalentTo(new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"D:\SteamLibrary"
            });
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
