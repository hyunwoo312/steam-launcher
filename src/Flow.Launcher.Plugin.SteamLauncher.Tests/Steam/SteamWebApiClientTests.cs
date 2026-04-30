using System.Net;
using System.Net.Http;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Tests.Fakes;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Steam;

public sealed class SteamWebApiClientTests
{
    private const string ServerInfoOk =
        """{"response":{"server_time":1714312345,"server_time_string":"Sat May  4 12:34:56 2024"}}""";

    private static SteamWebApiClient Build(FakeHttpMessageHandler handler, IApiKeyStore? keyStore = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.steampowered.com/") };
        return new SteamWebApiClient(http, keyStore ?? Substitute.For<IApiKeyStore>());
    }

    [Fact]
    public void HasApiKey_DelegatesToKeyStore()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        var client = Build(new FakeHttpMessageHandler(), keyStore);

        client.HasApiKey.Should().BeTrue();
    }

    [Fact]
    public async Task CheckHealthAsync_OkResponse_ReturnsTrue()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, ServerInfoOk);
        var client = Build(fake);

        var ok = await client.CheckHealthAsync(CancellationToken.None);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task CheckHealthAsync_NetworkFailure_ReturnsFalse()
    {
        var fake = new FakeHttpMessageHandler().EnqueueException(new HttpRequestException("offline"));
        var client = Build(fake);

        var ok = await client.CheckHealthAsync(CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_FiveHundred_ReturnsFalse()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.InternalServerError);
        var client = Build(fake);

        var ok = await client.CheckHealthAsync(CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_HitsServerInfoEndpoint()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, ServerInfoOk);
        var client = Build(fake);

        await client.CheckHealthAsync(CancellationToken.None);

        fake.ReceivedRequests.Should().ContainSingle()
            .Which.RequestUri!.AbsolutePath.Should().Be("/ISteamWebAPIUtil/GetServerInfo/v1/");
    }

    private const string OwnedGamesOk =
        """{"response":{"game_count":2,"games":[{"appid":730,"name":"CS2","playtime_forever":120,"rtime_last_played":1714000000},{"appid":570,"name":"Dota 2","playtime_forever":0}]}}""";

    private const string PlayerSummariesOk =
        """{"response":{"players":[{"steamid":"76561198000000001","personaname":"Test","personastate":1,"avatarfull":"https://example/a.jpg"}]}}""";

    private const string LevelOk = """{"response":{"player_level":42}}""";

    private const string StoreSearchOk =
        """{"items":[{"id":730,"name":"Counter-Strike 2","tiny_image":"https://example/cs2.jpg","price":{"currency":"USD","initial":1999,"final":1499,"discount_percent":25}}],"total":1}""";

    [Fact]
    public async Task GetOwnedGamesAsync_NoKeyConfigured_ReturnsNull()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        keyStore.Load().Returns((string?)null);
        var fake = new FakeHttpMessageHandler();
        var client = Build(fake, keyStore);

        var result = await client.GetOwnedGamesAsync(76561198000000001UL, CancellationToken.None);

        result.Should().BeNull();
        fake.ReceivedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOwnedGamesAsync_WithKey_HitsCorrectEndpoint()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, OwnedGamesOk);
        var client = Build(fake, keyStore);

        var result = await client.GetOwnedGamesAsync(76561198000000001UL, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Response!.GameCount.Should().Be(2);
        result.Response.Games.Should().HaveCount(2);
        fake.ReceivedRequests.Should().ContainSingle()
            .Which.RequestUri!.AbsolutePath.Should().Be("/IPlayerService/GetOwnedGames/v1/");
    }

    [Fact]
    public async Task GetPlayerSummariesAsync_PassesCommaJoinedIds()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, PlayerSummariesOk);
        var client = Build(fake, keyStore);

        await client.GetPlayerSummariesAsync(
            [76561198000000001UL, 76561198000000002UL],
            CancellationToken.None);

        var query = fake.ReceivedRequests[0].RequestUri!.Query;
        query.Should().Contain("steamids=76561198000000001%2C76561198000000002");
    }

    [Fact]
    public async Task GetSteamLevelAsync_NetworkFailure_ReturnsNull()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler().EnqueueException(new HttpRequestException("offline"));
        var client = Build(fake, keyStore);

        var result = await client.GetSteamLevelAsync(76561198000000001UL, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchStoreAsync_NoKeyRequired_HitsCorrectEndpoint()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, StoreSearchOk);
        var client = Build(fake, keyStore);

        var result = await client.SearchStoreAsync("counter strike", "us", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        var path = fake.ReceivedRequests[0].RequestUri!.AbsolutePath;
        path.Should().Be("/api/storesearch/");
    }

    private const string FriendListOk =
        """{"friendslist":{"friends":[{"steamid":"76561198000000002","relationship":"friend","friend_since":1714000000},{"steamid":"76561198000000003","relationship":"friend","friend_since":1715000000}]}}""";

    private const string PlayerSummariesChunkOk =
        """{"response":{"players":[{"steamid":"76561198000000002","personaname":"A","personastate":1},{"steamid":"76561198000000003","personaname":"B","personastate":0}]}}""";

    [Fact]
    public async Task GetFriendListAsync_NoKey_ReturnsNull()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(false);
        keyStore.Load().Returns((string?)null);
        var fake = new FakeHttpMessageHandler();
        var client = Build(fake, keyStore);

        var result = await client.GetFriendListAsync(76561198000000001UL, CancellationToken.None);

        result.Should().BeNull();
        fake.ReceivedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriendListAsync_WithKey_HitsCorrectEndpointAndDeserializes()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, FriendListOk);
        var client = Build(fake, keyStore);

        var result = await client.GetFriendListAsync(76561198000000001UL, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FriendsList!.Friends.Should().HaveCount(2);
        var path = fake.ReceivedRequests[0].RequestUri!.AbsolutePath;
        path.Should().Be("/ISteamUser/GetFriendList/v1/");
        fake.ReceivedRequests[0].RequestUri!.Query.Should().Contain("relationship=friend");
    }

    [Fact]
    public async Task GetPlayerSummariesAsync_OverHundredIds_IssuesMultipleChunkedRequests()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk)
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk)
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk);
        var client = Build(fake, keyStore);

        var ids = Enumerable.Range(0, 250).Select(i => 76561198000000000UL + (ulong)i).ToList();

        var result = await client.GetPlayerSummariesAsync(ids, CancellationToken.None);

        result.Should().NotBeNull();
        fake.ReceivedRequests.Should().HaveCount(3);
        foreach (var req in fake.ReceivedRequests)
        {
            var query = req.RequestUri!.Query;
            var commaCount = query.Split("%2C", StringSplitOptions.None).Length - 1;
            commaCount.Should().BeLessThan(100);
        }
    }

    [Fact]
    public async Task GetPlayerSummariesAsync_ChunkFailure_ReturnsNull()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk)
            .EnqueueException(new HttpRequestException("offline"));
        var client = Build(fake, keyStore);

        var ids = Enumerable.Range(0, 150).Select(i => 76561198000000000UL + (ulong)i).ToList();

        var result = await client.GetPlayerSummariesAsync(ids, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlayerSummariesAsync_ConcatenatesPlayersAcrossChunks()
    {
        var keyStore = Substitute.For<IApiKeyStore>();
        keyStore.IsConfigured.Returns(true);
        keyStore.Load().Returns("ABCDEF");
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk)
            .EnqueueStatus(HttpStatusCode.OK, PlayerSummariesChunkOk);
        var client = Build(fake, keyStore);

        var ids = Enumerable.Range(0, 150).Select(i => 76561198000000000UL + (ulong)i).ToList();

        var result = await client.GetPlayerSummariesAsync(ids, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Response!.Players.Should().HaveCount(4);
    }
}
