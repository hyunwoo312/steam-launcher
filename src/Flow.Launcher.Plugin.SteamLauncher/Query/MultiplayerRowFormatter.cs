using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Services;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal static class MultiplayerRowFormatter
{
    public static string BuildSubtitle(string friendPersonaName, SharedMultiplayerGame game)
    {
        var myH = CompactHours(game.MyPlaytimeMinutes);
        var friendH = CompactHours(game.FriendPlaytimeMinutes);
        var playtimes = $"You: {myH} · {friendPersonaName}: {friendH}";

        if (game.MyLastPlayed is { } mine && game.FriendLastPlayed is { } theirs)
        {
            var oldest = mine < theirs ? mine : theirs;
            return $"{playtimes} · played {CompactRelative(oldest)}";
        }

        return playtimes;
    }

    private static string CompactHours(long minutes)
    {
        if (minutes <= 0) return "0h";
        var hours = minutes / 60;
        if (hours == 0) return $"{minutes}m";
        return $"{hours}h";
    }

    private static string CompactRelative(DateTimeOffset when)
    {
        var ago = DateTimeOffset.UtcNow - when;
        if (ago.TotalDays < 1) return "today";
        if (ago.TotalDays < 2) return "yesterday";
        var days = (int)ago.TotalDays;
        if (days < 7) return $"{days}d ago";
        if (days < 30) return $"{days / 7}w ago";
        if (days < 365) return $"{days / 30}mo ago";
        return $"{days / 365}y ago";
    }
}
