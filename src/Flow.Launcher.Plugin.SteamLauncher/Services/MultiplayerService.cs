using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class MultiplayerService(
    IFriendsService friendsService,
    IOwnedGamesService ownedGamesService,
    ISteamWebApiClient client,
    IGameMetadataService metadata,
    IFuzzyMatcher fuzzyMatcher) : IMultiplayerService
{
    public async Task<MultiplayerResult> FindSharedAsync(string friendNameQuery, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(friendNameQuery)) return MultiplayerResult.NoFriendMatch();

        var friends = await friendsService.GetFriendsAsync(ct).ConfigureAwait(false);

        Friend? best = null;
        var bestScore = 0;
        foreach (var friend in friends)
        {
            var match = fuzzyMatcher.Match(friendNameQuery, friend.PersonaName);
            if (match.Score > bestScore)
            {
                best = friend;
                bestScore = match.Score;
            }
        }

        if (best is null) return MultiplayerResult.NoFriendMatch();

        var friendOwnedResponse = await client.GetOwnedGamesAsync(best.SteamId64, ct).ConfigureAwait(false);
        var friendGames = friendOwnedResponse?.Response?.Games;
        if (friendGames is null || friendGames.Count == 0)
            return MultiplayerResult.PrivateOrEmpty(best.PersonaName, best.SteamId64, best);

        var myOwned = await ownedGamesService.GetOwnedGamesAsync(ct).ConfigureAwait(false);
        if (myOwned.Count == 0)
            return MultiplayerResult.Match(best.PersonaName, best.SteamId64, [], best);

        var friendLastPlayedById = friendGames.ToDictionary(
            g => g.AppId,
            g => g.RtimeLastPlayed is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(g.RtimeLastPlayed.Value)
                : (DateTimeOffset?)null);
        var friendPlaytimeById = friendGames.ToDictionary(g => g.AppId, g => (long)g.PlaytimeForever);

        var sharedAppIds = myOwned
            .Where(m => friendLastPlayedById.ContainsKey(m.AppId))
            .ToList();

        var metadataLookup = await Task.WhenAll(
            sharedAppIds.Select(async g => (g.AppId, Meta: await metadata.GetAsync(g.AppId, ct).ConfigureAwait(false))))
            .ConfigureAwait(false);
        var metaById = metadataLookup.ToDictionary(t => t.AppId, t => t.Meta);

        var multiplayerCategories = MultiplayerCategoryIds.All;
        var multiplayer = sharedAppIds
            .Where(g => metaById[g.AppId].CategoryIds.Any(id => multiplayerCategories.Contains(id)))
            .ToList();

        var currentGameAppId = best.CurrentGameAppId;
        var rows = multiplayer
            .Select(g => new SharedMultiplayerGame(
                g.AppId,
                g.Name,
                g.PlaytimeMinutes,
                friendPlaytimeById[g.AppId],
                g.LastPlayed,
                friendLastPlayedById[g.AppId]))
            .OrderByDescending(g => currentGameAppId.HasValue && g.AppId == currentGameAppId.Value)
            .ThenByDescending(g => Math.Min(g.MyPlaytimeMinutes, g.FriendPlaytimeMinutes))
            .ThenByDescending(g => Math.Max(g.MyPlaytimeMinutes, g.FriendPlaytimeMinutes))
            .ThenByDescending(g => MinPlayedTimestamp(g.MyLastPlayed, g.FriendLastPlayed) ?? long.MinValue)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return MultiplayerResult.Match(best.PersonaName, best.SteamId64, rows, best);
    }

    private static long? MinPlayedTimestamp(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null || b is null) return null;
        var aTs = a.Value.ToUnixTimeSeconds();
        var bTs = b.Value.ToUnixTimeSeconds();
        return Math.Min(aTs, bTs);
    }
}
