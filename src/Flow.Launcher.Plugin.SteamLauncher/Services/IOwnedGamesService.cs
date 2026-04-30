namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IOwnedGamesService
{
    /// <summary>
    /// Returns the set of owned app IDs for the configured user, or an empty set when
    /// no API key / Steam ID is set or the call fails.
    /// </summary>
    Task<IReadOnlySet<uint>> GetOwnedAppIdsAsync(CancellationToken ct);

    /// <summary>
    /// Full per-game data (name, playtime, last-played) for the configured user. Empty list
    /// on failure or no config.
    /// </summary>
    Task<IReadOnlyList<OwnedGameSummary>> GetOwnedGamesAsync(CancellationToken ct);
}

public sealed record OwnedGameSummary(uint AppId, string Name, long PlaytimeMinutes, DateTimeOffset? LastPlayed);
