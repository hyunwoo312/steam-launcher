namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

/// <summary>
/// Resolves a remote avatar URL to a local path on disk, downloading on first miss.
/// Returns null if the URL is missing or the download fails — callers fall back to a default icon.
/// </summary>
public interface IAvatarCache
{
    Task<string?> GetLocalPathAsync(string? avatarUrl, CancellationToken ct);
}
