using System.IO;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Services;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal static class PreviewBuilders
{
    public static Result.PreviewInfo Game(InstalledGame game, GameMetadata meta, int friendsPlaying, string? imagePath)
    {
        var lines = new List<string>
        {
            $"{game.Name} ({game.AppId})",
            $"Playtime: {SubtitleFormatters.Playtime(game.PlaytimeMinutes, game.PlaytimeLast2WeeksMinutes)}",
            $"Install size: {SubtitleFormatters.Bytes(game.SizeOnDiskBytes)}",
            $"Install path: {game.FullInstallPath}"
        };

        if (game.LastPlayed is not null)
            lines.Insert(2, $"Last played: {SubtitleFormatters.LastPlayedRelative(game.LastPlayed.Value)}");
        if (friendsPlaying > 0)
            lines.Add($"Friends playing now: {friendsPlaying}");

        return Build(lines, imagePath);
    }

    public static Result.PreviewInfo StoreGame(StoreGame game, GameMetadata meta, int friendsPlaying, string? imagePath)
    {
        var lines = new List<string>
        {
            $"{game.Name} ({game.AppId})",
            game.IsOwned ? "Owned, not installed" : $"Price: {SubtitleFormatters.Price(game.Price, game.Currency)}"
        };

        if (game.DiscountPercent is > 0)
            lines.Add($"Discount: -{game.DiscountPercent}%");
        if (game.LastPlayed is not null)
            lines.Add($"Last played: {SubtitleFormatters.LastPlayedRelative(game.LastPlayed.Value)}");
        if (friendsPlaying > 0)
            lines.Add($"Friends playing now: {friendsPlaying}");

        return Build(lines, imagePath);
    }

    public static Result.PreviewInfo Friend(Friend friend, bool isFavorite, string? imagePath)
    {
        var lines = new List<string>
        {
            friend.PersonaName,
            FriendRowFormatter.BuildSubtitle(friend, isFavorite)
        };

        if (friend.CurrentGameAppId is { } appId)
            lines.Add($"Current game AppID: {appId}");
        if (friend.LastLogoffUnix is { } unix)
            lines.Add($"Last seen: {SubtitleFormatters.LastPlayedRelative(DateTimeOffset.FromUnixTimeSeconds(unix))}");

        return Build(lines, imagePath);
    }

    public static Result.PreviewInfo Account(KnownAccount account, string status, string? imagePath)
    {
        var lines = new List<string>
        {
            account.PersonaName,
            $"Status: {status}",
            $"Account: {account.AccountName}",
            $"SteamID64: {account.SteamId64}",
            account.RememberPassword ? "Password remembered" : "Password not remembered"
        };

        if (account.LastLoginUnix is { } unix && unix > 0)
            lines.Insert(3, $"Last login: {SubtitleFormatters.LastPlayedRelative(DateTimeOffset.FromUnixTimeSeconds(unix))}");

        return Build(lines, imagePath);
    }

    public static Result.PreviewInfo UserProfile(UserProfile profile, string? imagePath)
    {
        var lines = new List<string>
        {
            profile.PersonaName,
            $"SteamID64: {profile.SteamId64}",
            $"Steam level: {profile.SteamLevel?.ToString() ?? "?"}",
            $"Owned games: {profile.OwnedGameCount}",
            $"Total playtime: {profile.TotalPlaytimeMinutes / 60} hrs",
            $"Recent activity: {profile.RecentlyPlayedCount} games, {profile.RecentPlaytimeMinutes / 60} hrs last 2 weeks"
        };

        return Build(lines, imagePath);
    }

    public static Result.PreviewInfo MultiplayerGame(SharedMultiplayerGame game, string friendPersonaName, string? imagePath)
    {
        var lines = new List<string>
        {
            $"{game.Name} ({game.AppId})",
            $"Your playtime: {SubtitleFormatters.Playtime(game.MyPlaytimeMinutes, null)}",
            $"{friendPersonaName}'s playtime: {SubtitleFormatters.Playtime(game.FriendPlaytimeMinutes, null)}"
        };

        if (game.MyLastPlayed is not null)
            lines.Add($"Your last played: {SubtitleFormatters.LastPlayedRelative(game.MyLastPlayed.Value)}");
        if (game.FriendLastPlayed is not null)
            lines.Add($"{friendPersonaName}'s last played: {SubtitleFormatters.LastPlayedRelative(game.FriendLastPlayed.Value)}");

        return Build(lines, imagePath);
    }

    private static Result.PreviewInfo Build(IReadOnlyList<string> lines, string? imagePath) => new()
    {
        PreviewImagePath = IsLocalImagePath(imagePath) ? imagePath : null,
        Description = string.Join(Environment.NewLine, lines)
    };

    private static bool IsLocalImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return false;
        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri))
            return uri.IsFile && File.Exists(uri.LocalPath);
        return File.Exists(imagePath);
    }

}
