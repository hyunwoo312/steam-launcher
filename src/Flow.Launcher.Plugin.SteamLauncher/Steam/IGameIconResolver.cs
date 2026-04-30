namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public interface IGameIconResolver
{
    /// <summary>
    /// Returns a path or URL to the best available icon for the given app ID.
    /// Prefers local <c>appcache/librarycache/&lt;appid&gt;/&lt;sha1&gt;.jpg</c> when available
    /// (the small square icon Steam ships with each game), then falls back to Steam's
    /// public CDN capsule image. Always returns a non-null string — Flow renders a
    /// blank icon if the URL 404s, which is no worse than no icon at all.
    /// </summary>
    string Resolve(uint appId);
}
