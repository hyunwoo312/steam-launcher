using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class LocalLibraryServiceTests : IDisposable
{
    private readonly string _tempLibrary;
    private readonly string _tempSteamRoot;
    private readonly ISteamPathResolver _pathResolver;
    private readonly IGameIconResolver _iconResolver = Substitute.For<IGameIconResolver>();
    private readonly IVdfParser _parser = new VdfParser();

    public LocalLibraryServiceTests()
    {
        _tempLibrary = Path.Combine(Path.GetTempPath(), "flow-steam-test-" + Guid.NewGuid());
        _tempSteamRoot = Path.Combine(Path.GetTempPath(), "flow-steam-test-root-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_tempLibrary, "steamapps"));
        Directory.CreateDirectory(_tempSteamRoot);

        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "appmanifest_730.acf"));
        File.WriteAllText(Path.Combine(_tempLibrary, "steamapps", "appmanifest_730.acf"), fixture);

        var corruptFixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "appmanifest_corrupt.acf"));
        File.WriteAllText(Path.Combine(_tempLibrary, "steamapps", "appmanifest_999.acf"), corruptFixture);

        _pathResolver = Substitute.For<ISteamPathResolver>();
        _pathResolver.GetLibraryPaths().Returns([_tempLibrary]);
        _pathResolver.GetSteamInstallPath().Returns(_tempSteamRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempLibrary))
            Directory.Delete(_tempLibrary, recursive: true);
        if (Directory.Exists(_tempSteamRoot))
            Directory.Delete(_tempSteamRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetInstalledGamesAsync_ParsesValidManifest_ReturnsGame()
    {
        using var service = new LocalLibraryService(_pathResolver, _parser, _iconResolver);

        var games = await service.GetInstalledGamesAsync(CancellationToken.None);

        games.Should().HaveCount(1);
        games[0].AppId.Should().Be(730u);
        games[0].Name.Should().Be("Counter-Strike 2");
        games[0].InstallDir.Should().Be("Counter-Strike Global Offensive");
        games[0].LibraryPath.Should().Be(_tempLibrary);
    }

    [Fact]
    public async Task GetInstalledGamesAsync_PopulatesIconPathFromResolver()
    {
        _iconResolver.Resolve(730u).Returns("https://example.com/icon.jpg");
        using var service = new LocalLibraryService(_pathResolver, _parser, _iconResolver);

        var games = await service.GetInstalledGamesAsync(CancellationToken.None);

        games[0].IconPath.Should().Be("https://example.com/icon.jpg");
    }

    [Fact]
    public async Task GetInstalledGamesAsync_NoPlaytimeAvailable_ReturnsNullPlaytime()
    {
        using var service = new LocalLibraryService(_pathResolver, _parser, _iconResolver);

        var games = await service.GetInstalledGamesAsync(CancellationToken.None);

        games[0].PlaytimeMinutes.Should().BeNull();
    }

    [Fact]
    public async Task GetInstalledGamesAsync_CorruptManifest_IsSkipped()
    {
        using var service = new LocalLibraryService(_pathResolver, _parser, _iconResolver);

        var games = await service.GetInstalledGamesAsync(CancellationToken.None);

        games.Should().HaveCount(1);
        games[0].AppId.Should().Be(730u);
    }

    [Fact]
    public async Task GetInstalledGamesAsync_SteamworksRedistributables_AreFiltered()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempLibrary, "steamapps", "appmanifest_228980.acf"),
            """
            "AppState"
            {
                "appid"  "228980"
                "name"   "Steamworks Common Redistributables"
                "installdir" "Steamworks Shared"
                "StateFlags" "4"
            }
            """);
        using var service = new LocalLibraryService(_pathResolver, _parser, _iconResolver);

        var games = await service.GetInstalledGamesAsync(CancellationToken.None);

        games.Should().ContainSingle();
        games[0].AppId.Should().Be(730u);
    }

    [Fact]
    public async Task GetInstalledGamesAsync_SecondCall_UsesCache()
    {
        var pathResolverSpy = Substitute.For<ISteamPathResolver>();
        pathResolverSpy.GetLibraryPaths().Returns([_tempLibrary]);
        pathResolverSpy.GetSteamInstallPath().Returns(_tempSteamRoot);
        using var service = new LocalLibraryService(pathResolverSpy, _parser, _iconResolver);

        await service.GetInstalledGamesAsync(CancellationToken.None);
        await service.GetInstalledGamesAsync(CancellationToken.None);

        pathResolverSpy.Received(1).GetLibraryPaths();
    }

    [Fact]
    public async Task InvalidateCache_ForcesReloadOnNextCall()
    {
        var pathResolverSpy = Substitute.For<ISteamPathResolver>();
        pathResolverSpy.GetLibraryPaths().Returns([_tempLibrary]);
        pathResolverSpy.GetSteamInstallPath().Returns(_tempSteamRoot);
        using var service = new LocalLibraryService(pathResolverSpy, _parser, _iconResolver);

        await service.GetInstalledGamesAsync(CancellationToken.None);
        service.InvalidateCache();
        await service.GetInstalledGamesAsync(CancellationToken.None);

        pathResolverSpy.Received(2).GetLibraryPaths();
    }
}
