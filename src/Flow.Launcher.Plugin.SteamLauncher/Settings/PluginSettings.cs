namespace Flow.Launcher.Plugin.SteamLauncher.Settings;

public sealed class PluginSettings
{
    public string? SteamId64 { get; set; }
    public string? PreferredCountryCode { get; set; } = "us";
    public CacheSettings Cache { get; set; } = new();
#pragma warning disable CA2227
    public List<ulong> FavoriteFriendIds { get; set; } = [];
#pragma warning restore CA2227
}

public sealed class CacheSettings
{
    public bool PersistToDisk { get; set; } = true;
}
