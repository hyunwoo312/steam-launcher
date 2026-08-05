using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Cache;

public sealed class MemoryCacheStoreTests
{
    private static MemoryCacheStore Cache(params CachePolicy[] policies) =>
        new(policies, persistenceDir: null, clock: () => DateTimeOffset.UtcNow);

    [Fact]
    public void TryGet_MissingEntry_ReturnsFalse()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));

        cache.TryGet<string>("d", "k", out _).Should().BeFalse();
    }

    [Fact]
    public void Set_Then_TryGet_ReturnsValue()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));

        cache.Set("d", "k", "hello");

        cache.TryGet<string>("d", "k", out var v).Should().BeTrue();
        v.Should().Be("hello");
    }

    [Fact]
    public void Set_ExpiresAfterSuccessTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var cache = new MemoryCacheStore(
            [new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60))],
            persistenceDir: null,
            clock: () => now);

        cache.Set("d", "k", "hello");
        now = now.AddSeconds(61);

        cache.TryGet<string>("d", "k", out _).Should().BeFalse();
    }

    [Fact]
    public void SetFailure_PreventsTryGetButNotHasRecentFailure()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30)));

        cache.SetFailure("d", "k");

        cache.TryGet<string>("d", "k", out _).Should().BeFalse();
        cache.HasRecentFailure("d", "k").Should().BeTrue();
    }

    [Fact]
    public void SetFailure_ExpiresAfterFailureTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var cache = new MemoryCacheStore(
            [new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10))],
            persistenceDir: null,
            clock: () => now);

        cache.SetFailure("d", "k");
        now = now.AddSeconds(11);

        cache.HasRecentFailure("d", "k").Should().BeFalse();
    }

    [Fact]
    public void Set_OverwritesExistingFailure()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));
        cache.SetFailure("d", "k");

        cache.Set("d", "k", "recovered");

        cache.HasRecentFailure("d", "k").Should().BeFalse();
        cache.TryGet<string>("d", "k", out var v).Should().BeTrue();
        v.Should().Be("recovered");
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));
        cache.Set("d", "k", "hello");

        cache.Invalidate("d", "k");

        cache.TryGet<string>("d", "k", out _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateDomain_RemovesAllKeysInDomain_PreservesOthers()
    {
        var cache = Cache(
            new CachePolicy("a", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)),
            new CachePolicy("b", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));
        cache.Set("a", "k1", "alpha1");
        cache.Set("a", "k2", "alpha2");
        cache.Set("b", "k1", "bravo1");

        cache.InvalidateDomain("a");

        cache.TryGet<string>("a", "k1", out _).Should().BeFalse();
        cache.TryGet<string>("a", "k2", out _).Should().BeFalse();
        cache.TryGet<string>("b", "k1", out var v).Should().BeTrue();
        v.Should().Be("bravo1");
    }

    [Fact]
    public void Set_UnknownDomain_Throws()
    {
        var cache = Cache(new CachePolicy("d", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));

        var act = () => cache.Set("unknown", "k", "v");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown*");
    }

    [Fact]
    public void Dispose_PersistsEntriesToDisk_NewInstanceLoadsThem()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policy = new CachePolicy("d", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            using (var first = new MemoryCacheStore([policy], dir))
            {
                first.Set("d", "k1", "alpha");
                first.Set("d", "k2", "bravo");
            }

            using var second = new MemoryCacheStore([policy], dir);

            second.TryGet<string>("d", "k1", out var v1).Should().BeTrue();
            v1.Should().Be("alpha");
            second.TryGet<string>("d", "k2", out var v2).Should().BeTrue();
            v2.Should().Be("bravo");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Persistence_DropsExpiredEntriesOnLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var fakeNow = DateTimeOffset.UtcNow;
            var policy = new CachePolicy("d", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            using (var first = new MemoryCacheStore([policy], dir, () => fakeNow))
            {
                first.Set("d", "old", "stale");
            }

            fakeNow = fakeNow.AddSeconds(120);

            using var second = new MemoryCacheStore([policy], dir, () => fakeNow);

            second.TryGet<string>("d", "old", out _).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Persistence_PerDomainFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policies = new[]
            {
                new CachePolicy("alpha", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)),
                new CachePolicy("bravo", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60))
            };

            using (var cache = new MemoryCacheStore(policies, dir))
            {
                cache.Set("alpha", "a", "value-a");
                cache.Set("bravo", "b", "value-b");
            }

            File.Exists(Path.Combine(dir, "cache_alpha.json")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "cache_bravo.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // Every production caller stores a typed payload, never a bare string, so this is the
    // case that actually has to survive a restart.
    [Fact]
    public void Persistence_RoundTripsTypedPayload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policy = new CachePolicy("d", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            using (var first = new MemoryCacheStore([policy], dir))
            {
                first.Set("d", "730", new List<CachedItem>
                {
                    new(730, "Counter-Strike 2"),
                    new(440, "Team Fortress 2")
                });
            }

            using var second = new MemoryCacheStore([policy], dir);

            second.TryGet<List<CachedItem>>("d", "730", out var items).Should().BeTrue();
            items.Should().HaveCount(2);
            items![0].Should().Be(new CachedItem(730, "Counter-Strike 2"));
            items[1].Should().Be(new CachedItem(440, "Team Fortress 2"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Persistence_RoundTripsFailureMarkers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policy = new CachePolicy("d", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            using (var first = new MemoryCacheStore([policy], dir))
            {
                first.SetFailure("d", "dead");
            }

            using var second = new MemoryCacheStore([policy], dir);

            second.HasRecentFailure("d", "dead").Should().BeTrue();
            second.TryGet<CachedItem>("d", "dead", out _).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Persistence_ReadingAsWrongType_DoesNotThrowAndDropsEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policy = new CachePolicy("d", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            using (var first = new MemoryCacheStore([policy], dir))
            {
                first.Set("d", "k", new List<CachedItem> { new(1, "one") });
            }

            using var second = new MemoryCacheStore([policy], dir);

            second.TryGet<CachedItem>("d", "k", out _).Should().BeFalse();
            second.TryGet<List<CachedItem>>("d", "k", out _).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Persistence_UnreadEntriesSurviveASecondRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cache-persist-" + Guid.NewGuid());
        try
        {
            var policy = new CachePolicy("d", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            using (var first = new MemoryCacheStore([policy], dir))
                first.Set("d", "k", new List<CachedItem> { new(7, "seven") });

            // Loaded but never read, so it is still unmaterialized when saved again.
            using (new MemoryCacheStore([policy], dir)) { }

            using var third = new MemoryCacheStore([policy], dir);

            third.TryGet<List<CachedItem>>("d", "k", out var items).Should().BeTrue();
            items.Should().ContainSingle().Which.Should().Be(new CachedItem(7, "seven"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public sealed record CachedItem(uint AppId, string Name);
}
