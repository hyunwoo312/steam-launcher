using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public sealed class MemoryCacheStore : ICacheStore, IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
        value = null;

        if (!_entries.TryGetValue((domain, key), out var entry)) return false;
        if (entry.IsFailure || entry.ExpiresAt <= _clock()) return false;

        switch (entry.Value)
        {
            case T typed:
                value = typed;
                return true;

            // Rehydrated from disk but not yet materialized. Nothing on disk records the
            // payload's type, so the caller's T is what names it — which also means a
            // caller can never be handed a type it did not ask for.
            case PendingPayload pending:
                T? materialized;
                try
                {
                    materialized = JsonSerializer.Deserialize<T>(pending.Json);
                }
                catch (JsonException)
                {
                    materialized = null;
                }
                catch (NotSupportedException)
                {
                    materialized = null;
                }

                if (materialized is null)
                {
                    _entries.TryRemove((domain, key), out _);
                    return false;
                }

                _entries[(domain, key)] = entry with { Value = materialized };
                value = materialized;
                return true;

            default:
                return false;
        }
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
                var json = File.ReadAllText(path, Encoding.UTF8);
                var snapshot = JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(json);
                if (snapshot is null) continue;

                foreach (var (key, entry) in snapshot)
                {
                    if (entry.ExpiresAt <= _clock()) continue;

                    // Failure markers carry no payload; a success entry without one is
                    // unusable, so drop it rather than resurrect an empty hit.
                    if (entry.IsFailure)
                    {
                        _entries[(domain, key)] = new Entry(null, entry.ExpiresAt, IsFailure: true);
                        continue;
                    }
                    if (string.IsNullOrEmpty(entry.Value)) continue;

                    _entries[(domain, key)] =
                        new Entry(new PendingPayload(entry.Value), entry.ExpiresAt, IsFailure: false);
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
            var snapshot = new Dictionary<string, PersistedEntry>();
            foreach (var kv in _entries.Where(kv => kv.Key.Domain == domain && kv.Value.ExpiresAt > _clock()))
            {
                if (kv.Value.IsFailure)
                {
                    snapshot[kv.Key.Key] = new PersistedEntry(null, kv.Value.ExpiresAt, IsFailure: true);
                    continue;
                }

                var payload = Serialize(kv.Value.Value);
                if (payload is null) continue;
                snapshot[kv.Key.Key] = new PersistedEntry(payload, kv.Value.ExpiresAt, IsFailure: false);
            }

            try
            {
                var json = JsonSerializer.Serialize(snapshot);
                File.WriteAllText(Path.Combine(_persistenceDir!, $"cache_{domain}.json"), json, Utf8NoBom);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Serializes a live cache value for disk. Entries loaded but never read this session
    /// are still holding their original JSON, so they pass straight through.
    /// </summary>
    private static string? Serialize(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case PendingPayload pending:
                return pending.Json;
            default:
                try
                {
                    return JsonSerializer.Serialize(value, value.GetType());
                }
                catch (NotSupportedException)
                {
                    return null;
                }
        }
    }

    private sealed record Entry(object? Value, DateTimeOffset ExpiresAt, bool IsFailure);

    /// <summary>A payload read from disk, still JSON because its type is not known until read.</summary>
    private sealed record PendingPayload(string Json);

    // Value holds the entry's payload as JSON. Failure markers persist with a null payload.
    private sealed record PersistedEntry(string? Value, DateTimeOffset ExpiresAt, bool IsFailure);
}
