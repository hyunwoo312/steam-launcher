using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Json;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class UserProfileServiceTests
{
    [Fact]
    public async Task GetMyProfileAsync_NoSteamIdConfigured_ReturnsNull()
    {
        var service = new UserProfileService(
            Substitute.For<ISteamWebApiClient>(),
            Substitute.For<IOwnedGamesService>(),
            new PluginSettings { SteamId64 = null });

        var result = await service.GetMyProfileAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyProfileAsync_HappyPath_AggregatesData()
    {
        var client = Substitute.For<ISteamWebApiClient>();
        client.GetPlayerSummariesAsync(Arg.Any<IEnumerable<ulong>>(), Arg.Any<CancellationToken>())
            .Returns(new PlayerSummariesResponse(new PlayerSummariesBody(
            [new PlayerSummary("76561198000000001", "TestUser", "https://example/a.jpg", 1, null, null, null)])));
        client.GetSteamLevelAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new SteamLevelResponse(new SteamLevelBody(42)));
        client.GetRecentlyPlayedAsync(Arg.Any<ulong>(), Arg.Any<CancellationToken>())
            .Returns(new RecentlyPlayedResponse(new RecentlyPlayedBody(2,
            [
                new OwnedGame(730, "CS2", 9000, 60, null),
                new OwnedGame(570, "Dota", 4000, 30, null)
            ])));
        var owned = Substitute.For<IOwnedGamesService>();
        owned.GetOwnedGamesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new OwnedGameSummary(730, "CS2", 9000, null),
                new OwnedGameSummary(570, "Dota", 4000, null),
                new OwnedGameSummary(440, "TF2", 0, null)
            ]);

        var service = new UserProfileService(
            client,
            owned,
            new PluginSettings { SteamId64 = "76561198000000001" });

        var profile = await service.GetMyProfileAsync(CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.PersonaName.Should().Be("TestUser");
        profile.SteamLevel.Should().Be(42);
        profile.OwnedGameCount.Should().Be(3);
        profile.TotalPlaytimeMinutes.Should().Be(13000);
        profile.RecentlyPlayedCount.Should().Be(2);
        profile.RecentPlaytimeMinutes.Should().Be(90);
    }
}
