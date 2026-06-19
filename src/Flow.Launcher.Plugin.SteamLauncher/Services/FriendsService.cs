using System.Globalization;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class FriendsService(
    ISteamWebApiClient client,
    ICacheStore cache,
    PluginSettings settings) : IFriendsService
{
    private const string CacheKey = "all";

    public async Task<IReadOnlyList<Friend>> GetFriendsAsync(CancellationToken ct)
    {
        if (!ulong.TryParse(settings.SteamId64, out var steamId)) return [];

        if (cache.TryGet<List<Friend>>(CachePolicies.FriendList, CacheKey, out var cached))
            return cached;

        if (cache.HasRecentFailure(CachePolicies.FriendList, CacheKey))
            return [];

        var listResponse = await client.GetFriendListAsync(steamId, ct).ConfigureAwait(false);
        var entries = listResponse?.FriendsList?.Friends;
        if (entries is null)
        {
            cache.SetFailure(CachePolicies.FriendList, CacheKey);
            return [];
        }

        if (entries.Count == 0)
        {
            cache.Set(CachePolicies.FriendList, CacheKey, new List<Friend>());
            return [];
        }

        var friendIds = entries
            .Select(e => ulong.TryParse(e.SteamId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0UL)
            .Where(id => id != 0UL)
            .ToList();

        var summaries = await client.GetPlayerSummariesAsync(friendIds, ct).ConfigureAwait(false);
        var players = summaries?.Response?.Players;
        if (players is null)
        {
            cache.SetFailure(CachePolicies.FriendList, CacheKey);
            return [];
        }

        var byId = new Dictionary<ulong, Json.PlayerSummary>();
        foreach (var p in players)
        {
            if (ulong.TryParse(p.SteamId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0UL)
                byId[id] = p;
        }

        var hydrated = friendIds
            .Where(id => byId.ContainsKey(id))
            .Select(id => Build(id, byId[id]))
            .ToList();

        cache.Set(CachePolicies.FriendList, CacheKey, hydrated);
        return hydrated;
    }

    public async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            await GetFriendsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { }
    }

    private static Friend Build(ulong steamId, Json.PlayerSummary summary)
    {
        var personaState = (PersonaState)(summary.PersonaState ?? 0);
        var appId = uint.TryParse(summary.GameId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (uint?)null;

        return new Friend
        {
            SteamId64 = steamId,
            PersonaName = summary.PersonaName ?? $"Steam {steamId}",
            AvatarUrl = summary.AvatarUrl,
            PersonaState = personaState,
            CurrentGameAppId = appId,
            CurrentGameName = summary.GameExtraInfo,
            LastLogoffUnix = summary.LastLogoff
        };
    }
}
