using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public sealed class AvatarCache : IAvatarCache
{
    private readonly string _root;
    private readonly HttpClient _http;
    private readonly int _maxEntries;

    public AvatarCache(string root, HttpClient http, int maxEntries = 1000)
    {
        _root = root;
        _http = http;
        _maxEntries = maxEntries;
        Directory.CreateDirectory(_root);
    }

    public async Task<string?> GetLocalPathAsync(string? avatarUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(avatarUrl)) return null;

        var path = PathFor(avatarUrl);
        if (File.Exists(path)) return path;

        try
        {
            using var response = await _http.GetAsync(avatarUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);

            EvictIfOverCap();
            return path;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private string PathFor(string url)
    {
#pragma warning disable CA5350
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
#pragma warning restore CA5350
        var hex = Convert.ToHexStringLower(hash);
        return Path.Combine(_root, $"{hex}.jpg");
    }

    private void EvictIfOverCap()
    {
        try
        {
            var files = Directory.EnumerateFiles(_root)
                .Select(p => new FileInfo(p))
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            var excess = files.Count - _maxEntries;
            for (var i = 0; i < excess; i++)
            {
                try { files[i].Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
