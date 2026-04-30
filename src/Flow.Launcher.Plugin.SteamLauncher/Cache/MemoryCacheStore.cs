using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public sealed class MemoryCacheStore : ICacheStore, IDisposable
{
    private readonly Dictionary<string, CachePolicy> _policies;
    private readonly ConcurrentDictionary<(string Domain, string Key), Entry> _entries = new();
    private readonly Func<DateTimeOffset> _clock;
    private readonly string? _persistenceDir;
    private bool _disposed;

    public MemoryCacheStore(
        IEnumerable<CachePolicy> policies,
        string? persistenceDir,
        Func<DateTimeOffset>? clock = null)
    {
        _policies = policies.ToDictionary(p => p.Domain, p => p);
        _persistenceDir = persistenceDir;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        if (_persistenceDir is not null) LoadFromDisk();
    }

    public bool TryGet<T>(string domain, string key, [NotNullWhen(true)] out T? value) where T : class
    {
        if (_entries.TryGetValue((domain, key), out var entry)
            && !entry.IsFailure
            && entry.ExpiresAt > _clock()
            && entry.Value is T typed)
        {
            value = typed;
            return true;
        }
        value = null;
        return false;
    }

    public void Set<T>(string domain, string key, T value) where T : class
    {
        var policy = RequirePolicy(domain);
        _entries[(domain, key)] = new Entry(value, _clock() + policy.SuccessTtl, IsFailure: false);
    }

    public void SetFailure(string domain, string key)
    {
        var policy = RequirePolicy(domain);
        _entries[(domain, key)] = new Entry(null, _clock() + policy.FailureTtl, IsFailure: true);
    }

    public bool HasRecentFailure(string domain, string key) =>
        _entries.TryGetValue((domain, key), out var e) && e.IsFailure && e.ExpiresAt > _clock();

    public void Invalidate(string domain, string key) =>
        _entries.TryRemove((domain, key), out _);

    public void InvalidateDomain(string domain)
    {
        foreach (var k in _entries.Keys.Where(k => k.Domain == domain).ToList())
            _entries.TryRemove(k, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_persistenceDir is not null) SaveToDisk();
    }

    private CachePolicy RequirePolicy(string domain) =>
        _policies.TryGetValue(domain, out var p)
            ? p
            : throw new InvalidOperationException($"No CachePolicy registered for domain '{domain}'.");

    private void LoadFromDisk()
    {
        if (!Directory.Exists(_persistenceDir!)) return;

        foreach (var domain in _policies.Keys)
        {
            var path = Path.Combine(_persistenceDir!, $"cache_{domain}.json");
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                var snapshot = JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(json);
                if (snapshot is null) continue;

                foreach (var (key, entry) in snapshot)
                {
                    if (entry.ExpiresAt <= _clock()) continue;
                    _entries[(domain, key)] = new Entry(entry.Value, entry.ExpiresAt, entry.IsFailure);
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void SaveToDisk()
    {
        Directory.CreateDirectory(_persistenceDir!);
        foreach (var domain in _policies.Keys)
        {
            var snapshot = _entries
                .Where(kv => kv.Key.Domain == domain && kv.Value.ExpiresAt > _clock())
                .ToDictionary(
                    kv => kv.Key.Key,
                    kv => new PersistedEntry(
                        kv.Value.Value as string,
                        kv.Value.ExpiresAt,
                        kv.Value.IsFailure));
            try
            {
                var json = JsonSerializer.Serialize(snapshot);
                File.WriteAllText(Path.Combine(_persistenceDir!, $"cache_{domain}.json"), json);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed record Entry(object? Value, DateTimeOffset ExpiresAt, bool IsFailure);

    // String-only on disk; services serialize their own typed payloads via System.Text.Json.
    private sealed record PersistedEntry(string? Value, DateTimeOffset ExpiresAt, bool IsFailure);
}
