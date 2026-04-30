using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Security;

public sealed class DpapiApiKeyStoreTests : IDisposable
{
    private readonly string _tempDir;

    public DpapiApiKeyStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "flow-steam-keystore-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_WhenNothingSaved_ReturnsNull()
    {
        var store = new DpapiApiKeyStore(_tempDir);

        store.Load().Should().BeNull();
        store.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheKey()
    {
        var store = new DpapiApiKeyStore(_tempDir);

        store.Save("ABCDEF1234567890ABCDEF1234567890");

        store.Load().Should().Be("ABCDEF1234567890ABCDEF1234567890");
        store.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Save_WithNull_ClearsExistingKey()
    {
        var store = new DpapiApiKeyStore(_tempDir);
        store.Save("foo");
        store.IsConfigured.Should().BeTrue();

        store.Save(null);

        store.Load().Should().BeNull();
        store.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Save_WithWhitespace_ClearsExistingKey()
    {
        var store = new DpapiApiKeyStore(_tempDir);
        store.Save("foo");

        store.Save("   ");

        store.Load().Should().BeNull();
    }

    [Fact]
    public void StoredFile_DoesNotContainPlaintextKey()
    {
        var store = new DpapiApiKeyStore(_tempDir);
        store.Save("VERY_OBVIOUS_PLAINTEXT_KEY");

        var bytes = File.ReadAllBytes(Path.Combine(_tempDir, "owned_api_key.bin"));
        var bytesAsAscii = System.Text.Encoding.ASCII.GetString(bytes);

        bytesAsAscii.Should().NotContain("VERY_OBVIOUS_PLAINTEXT_KEY");
    }

    [Fact]
    public void NewInstance_SeesPersistedKey()
    {
        new DpapiApiKeyStore(_tempDir).Save("persisted-key");

        var fresh = new DpapiApiKeyStore(_tempDir);

        fresh.Load().Should().Be("persisted-key");
    }

    [Fact]
    public void Load_WhenCiphertextIsCorrupt_ReturnsNullAndClearsOrphan()
    {
        var keyFile = Path.Combine(_tempDir, "owned_api_key.bin");
        File.WriteAllBytes(keyFile, [0x01, 0x02, 0x03, 0x04, 0x05]);

        var store = new DpapiApiKeyStore(_tempDir);

        store.Load().Should().BeNull();
        store.IsConfigured.Should().BeFalse();
        File.Exists(keyFile).Should().BeFalse();
    }
}
