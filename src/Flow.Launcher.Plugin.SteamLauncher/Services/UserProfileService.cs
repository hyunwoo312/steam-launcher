using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class UserProfileService(
    ISteamWebApiClient client,
    IOwnedGamesService ownedGames,
    PluginSettings settings) : IUserProfileService
{
    public async Task<UserProfile?> GetMyProfileAsync(CancellationToken ct)
    {
        if (!ulong.TryParse(settings.SteamId64, out var steamId)) return null;

        var summariesTask = client.GetPlayerSummariesAsync([steamId], ct);
        var levelTask = client.GetSteamLevelAsync(steamId, ct);
        var ownedTask = ownedGames.GetOwnedGamesAsync(ct);
        var recentTask = client.GetRecentlyPlayedAsync(steamId, ct);

        await Task.WhenAll(summariesTask, levelTask, ownedTask, recentTask).ConfigureAwait(false);

        var summary = (await summariesTask.ConfigureAwait(false))?.Response?.Players?.FirstOrDefault();
        var level = (await levelTask.ConfigureAwait(false))?.Response?.PlayerLevel;
        var owned = await ownedTask.ConfigureAwait(false);
        var recent = (await recentTask.ConfigureAwait(false))?.Response?.Games ?? [];

        return new UserProfile
        {
            SteamId64 = steamId,
            PersonaName = summary?.PersonaName ?? $"Steam {steamId}",
            AvatarUrl = summary?.AvatarUrl,
            SteamLevel = level,
            OwnedGameCount = owned.Count,
            TotalPlaytimeMinutes = owned.Sum(g => g.PlaytimeMinutes),
            RecentlyPlayedCount = recent.Count,
            RecentPlaytimeMinutes = recent.Sum(g => (long)(g.Playtime2Weeks ?? 0))
        };
    }
}
