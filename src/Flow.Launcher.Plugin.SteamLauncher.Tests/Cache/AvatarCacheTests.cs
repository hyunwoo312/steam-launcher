using System.IO;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Tests.Fakes;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Cache;

public sealed class AvatarCacheTests : IDisposable
{
    private readonly string _root;

    public AvatarCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "avatar-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static AvatarCache Build(string root, FakeHttpMessageHandler handler, int maxEntries = 1000)
    {
        var http = new HttpClient(handler);
        return new AvatarCache(root, http, maxEntries);
    }

    [Fact]
    public async Task GetLocalPathAsync_EmptyUrl_ReturnsNull()
    {
        var cache = Build(_root, new FakeHttpMessageHandler());

        var path = await cache.GetLocalPathAsync(null, CancellationToken.None);

        path.Should().BeNull();
    }

    [Fact]
    public async Task GetLocalPathAsync_FirstCall_DownloadsAndWritesToDisk()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, "JPEG_BYTES_HERE");
        var cache = Build(_root, fake);

        var path = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);

        path.Should().NotBeNull();
        File.Exists(path!).Should().BeTrue();
        fake.ReceivedRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetLocalPathAsync_SecondCall_HitsDisk()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, "JPEG_BYTES_HERE");
        var cache = Build(_root, fake);

        var path1 = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);
        var path2 = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);

        path1.Should().Be(path2);
        fake.ReceivedRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetLocalPathAsync_DownloadFails_ReturnsNull()
    {
        var fake = new FakeHttpMessageHandler().EnqueueException(new HttpRequestException("offline"));
        var cache = Build(_root, fake);

        var path = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);

        path.Should().BeNull();
        Directory.GetFiles(_root).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocalPathAsync_PriorFileMissing_RedownloadsCleanly()
    {
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.OK, "FIRST")
            .EnqueueStatus(HttpStatusCode.OK, "SECOND");
        var cache = Build(_root, fake);

        var path = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);
        File.Delete(path!);

        var path2 = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);

        path2.Should().Be(path);
        File.Exists(path2!).Should().BeTrue();
        fake.ReceivedRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLocalPathAsync_OverCap_EvictsOldestByMtime()
    {
        var fake = new FakeHttpMessageHandler();
        for (var i = 0; i < 5; i++)
            fake.EnqueueStatus(HttpStatusCode.OK, $"BYTES_{i}");
        var cache = Build(_root, fake, maxEntries: 3);

        var p1 = await cache.GetLocalPathAsync("https://example.com/1.jpg", CancellationToken.None);
        await Task.Delay(50);
        var p2 = await cache.GetLocalPathAsync("https://example.com/2.jpg", CancellationToken.None);
        await Task.Delay(50);
        var p3 = await cache.GetLocalPathAsync("https://example.com/3.jpg", CancellationToken.None);
        await Task.Delay(50);

        await cache.GetLocalPathAsync("https://example.com/4.jpg", CancellationToken.None);
        await Task.Delay(50);
        await cache.GetLocalPathAsync("https://example.com/5.jpg", CancellationToken.None);

        File.Exists(p1!).Should().BeFalse();
        File.Exists(p2!).Should().BeFalse();
        File.Exists(p3!).Should().BeTrue();
        Directory.GetFiles(_root).Should().HaveCount(3);
    }

    [Fact]
    public async Task GetLocalPathAsync_Sha1FilenameStableAcrossCalls()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, "X");
        var cache = Build(_root, fake);

        var path = await cache.GetLocalPathAsync("https://example.com/a.jpg", CancellationToken.None);

        Path.GetFileName(path!).Should().MatchRegex("^[0-9a-f]{40}\\.jpg$");
    }
}
