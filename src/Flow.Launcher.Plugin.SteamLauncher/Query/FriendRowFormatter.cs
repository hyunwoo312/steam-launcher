using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public static class FriendRowFormatter
{
    public static string BuildSubtitle(Friend friend, bool isFavorite = false)
    {
        var prefix = isFavorite ? "⭐️ " : string.Empty;
        return prefix + BuildStatusSubtitle(friend);
    }

    private static string BuildStatusSubtitle(Friend friend)
    {
        if (friend.IsInGame)
        {
            var name = friend.CurrentGameName ?? $"AppID {friend.CurrentGameAppId}";
            return $"🎮 Playing {name}";
        }

        if (friend.PersonaState == PersonaState.Offline)
        {
            if (friend.LastLogoffUnix is { } unix)
            {
                var ago = SubtitleFormatters.LastPlayedRelative(DateTimeOffset.FromUnixTimeSeconds(unix));
                return $"⚫ Offline · last seen {ago}";
            }
            return "⚫ Offline";
        }

        return friend.PersonaState switch
        {
            PersonaState.Online         => "🟢 Online",
            PersonaState.Busy           => "🔴 Busy",
            PersonaState.Away           => "🟡 Away",
            PersonaState.Snooze         => "🟡 Snooze",
            PersonaState.LookingToTrade => "🟢 Looking to trade",
            PersonaState.LookingToPlay  => "🟢 Looking to play",
            _ => "⚫ Offline"
        };
    }
}
