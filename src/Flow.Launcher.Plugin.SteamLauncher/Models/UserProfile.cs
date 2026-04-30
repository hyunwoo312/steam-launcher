namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public sealed record UserProfile
{
    public required ulong SteamId64 { get; init; }
    public required string PersonaName { get; init; }
    public string? AvatarUrl { get; init; }
    public int? SteamLevel { get; init; }
    public int OwnedGameCount { get; init; }
    public long TotalPlaytimeMinutes { get; init; }
    public int RecentlyPlayedCount { get; init; }
    public long RecentPlaytimeMinutes { get; init; }
}
