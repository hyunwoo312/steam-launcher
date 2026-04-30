using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal static class LibraryRowFormatter
{
    public static string BuildSubtitle(InstalledGame game, GameMetadata meta, int friendsPlaying = 0)
    {
        var parts = new List<string>(6);

        var badge = game.GetInstallState() switch
        {
            InstallState.UpdateRequired => "⚠ Update available",
            InstallState.Updating       => "⬇ Updating",
            InstallState.UpdatePaused   => "⏸ Update paused",
            _ => null
        };
        if (badge is not null) parts.Add(badge);

        parts.Add(SubtitleFormatters.Playtime(game.PlaytimeMinutes, game.PlaytimeLast2WeeksMinutes));
        if (game.LastPlayed is not null)
            parts.Add(SubtitleFormatters.LastPlayedRelative(game.LastPlayed.Value));

        var reviews = SubtitleFormatters.Reviews(meta);
        if (reviews is not null) parts.Add(reviews);

        parts.Add(SubtitleFormatters.Bytes(game.SizeOnDiskBytes));

        if (friendsPlaying > 0)
            parts.Add($"{friendsPlaying} friend{(friendsPlaying == 1 ? "" : "s")} playing");

        return string.Join(" · ", parts);
    }
}
