using System.Diagnostics.CodeAnalysis;

namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public interface ICacheStore
{
    /// <summary>
    /// Tries to read a non-expired success entry. Returns false if missing, expired,
    /// or only a failure marker exists.
    /// </summary>
    bool TryGet<T>(string domain, string key, [NotNullWhen(true)] out T? value) where T : class;

    /// <summary>Stores a successful value for the domain's success-TTL window.</summary>
    void Set<T>(string domain, string key, T value) where T : class;

    /// <summary>
    /// Records that a fetch failed. Subsequent TryGet returns false; subsequent
    /// HasRecentFailure returns true within the failure-TTL window.
    /// </summary>
    void SetFailure(string domain, string key);

    /// <summary>True if a failure for this key was recorded inside the failure-TTL window.</summary>
    bool HasRecentFailure(string domain, string key);

    /// <summary>Removes one entry (success or failure) immediately.</summary>
    void Invalidate(string domain, string key);

    /// <summary>Removes all entries in a domain.</summary>
    void InvalidateDomain(string domain);
}
