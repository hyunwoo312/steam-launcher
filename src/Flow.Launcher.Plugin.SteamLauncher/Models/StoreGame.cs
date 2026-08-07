namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public sealed record StoreGame
{
    public required uint AppId { get; init; }
    public required string Name { get; init; }
    public string? IconUrl { get; init; }
    public decimal? Price { get; init; }
    public string? Currency { get; init; }
    public int? DiscountPercent { get; init; }
    public bool IsOwned { get; init; }

    /// <summary>
    /// When the user last launched this game. Populated for owned games (from
    /// <c>GetOwnedGames.rtime_last_played</c>); null for unowned-store-listing rows
    /// or owned games the user has never launched.
    /// </summary>
    public DateTimeOffset? LastPlayed { get; init; }

    /// <summary>
    /// Lifetime playtime in minutes, for owned games. Null for unowned store listings.
    /// </summary>
    public long? PlaytimeMinutes { get; init; }
}
