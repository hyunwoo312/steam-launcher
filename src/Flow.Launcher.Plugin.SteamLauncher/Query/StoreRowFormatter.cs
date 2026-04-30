using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal static class StoreRowFormatter
{
    public static string BuildSubtitle(StoreGame game, GameMetadata meta, int friendsPlaying = 0)
    {
        var parts = new List<string>(7);

        if (game.IsOwned)
        {
            parts.Add("⚙ Owned · not installed");
            parts.Add(SubtitleFormatters.LastPlayedForOwnedRow(game.LastPlayed));
        }
        else
        {
            parts.Add(SubtitleFormatters.IsFree(game.PriceUsd) ? "Free" : $"${game.PriceUsd:F2}");
            if (game.DiscountPercent is > 0) parts.Add($"-{game.DiscountPercent}%");
            if (!string.IsNullOrEmpty(meta.ReleaseDate)) parts.Add(meta.ReleaseDate);
        }

        var reviews = SubtitleFormatters.Reviews(meta);
        if (reviews is not null) parts.Add(reviews);
        if (!string.IsNullOrEmpty(meta.Developer)) parts.Add(meta.Developer);

        if (friendsPlaying > 0)
            parts.Add($"{friendsPlaying} friend{(friendsPlaying == 1 ? "" : "s")} playing");

        return string.Join(" · ", parts);
    }
}
