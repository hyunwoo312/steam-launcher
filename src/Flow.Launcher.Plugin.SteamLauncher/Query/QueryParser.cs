namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public static class QueryParser
{
    private const int MaxQueryLength = 200;

    public static ParsedQuery Parse(string? rawSearch)
    {
        var search = (rawSearch ?? string.Empty).Trim();
        if (search.Length > MaxQueryLength)
            search = search[..MaxQueryLength];

        if (search.Length == 0)
            return new ParsedQuery.Empty();

        if (search.Equals("me", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.Me();
        if (search.Equals("new", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.RecentlyAddedNeverPlayed();
        if (search.Equals("friends", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.FriendsList(null);
        if (search.StartsWith("friends ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.FriendsList(search[8..].Trim());

        if (search.Equals("api", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.ApiConfig(ApiConfigAction.ShowStatus, null);
        if (search.StartsWith("api id ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.ApiConfig(ApiConfigAction.SaveSteamId, search[7..].Trim());
        if (search.StartsWith("api ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.ApiConfig(ApiConfigAction.SaveKey, search[4..].Trim());

        if (search.Equals("status", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.StatusSwitcher();
        if (search.Equals("switch", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.AccountSwitcher();
        if (search.StartsWith("switch confirm ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.AccountSwitcher(search[15..].Trim());
        if (search.Equals("multi", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.MultiplayerWith(string.Empty);
        if (search.StartsWith("multi ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.MultiplayerWith(search[6..].Trim());

        if (search.Equals("verify", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.VerifyGame(null);
        if (search.StartsWith("verify ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.VerifyGame(search[7..].Trim());

        if (search.Equals("uninstall", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.UninstallGame(null);
        if (search.StartsWith("uninstall ", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.UninstallGame(search[10..].Trim());

        if (search.Equals("settings", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.OpenSteamWindow(SteamWindow.Settings);
        if (search.Equals("downloads", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.OpenSteamWindow(SteamWindow.Downloads);
        if (search.Equals("bigpicture", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.OpenSteamWindow(SteamWindow.BigPicture);
        if (search.Equals("screenshots", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.OpenSteamWindow(SteamWindow.Screenshots);
        if (search.Equals("redeem", StringComparison.OrdinalIgnoreCase))
            return new ParsedQuery.OpenSteamWindow(SteamWindow.Redeem);

        return new ParsedQuery.LibraryFilter(search);
    }
}
