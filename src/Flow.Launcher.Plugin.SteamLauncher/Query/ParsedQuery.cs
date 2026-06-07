namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public abstract record ParsedQuery
{
    public sealed record Empty : ParsedQuery;
    public sealed record LibraryFilter(string Term) : ParsedQuery;
    public sealed record StoreSearch(string Term) : ParsedQuery;
    public sealed record Me : ParsedQuery;
    public sealed record RecentlyAddedNeverPlayed : ParsedQuery;

    public sealed record ApiConfig(ApiConfigAction Action, string? Argument) : ParsedQuery;

    public sealed record FriendsList(string? Filter) : ParsedQuery;

    public sealed record StatusSwitcher : ParsedQuery;
    public sealed record AccountSwitcher(string? ConfirmAccountName = null) : ParsedQuery;
    public sealed record MultiplayerWith(string FriendName) : ParsedQuery;

    public sealed record VerifyGame(string? Filter) : ParsedQuery;
    public sealed record UninstallGame(string? Filter) : ParsedQuery;
    public sealed record OpenSteamSettings : ParsedQuery;
    public sealed record OpenSteamDownloads : ParsedQuery;
}

public enum ApiConfigAction
{
    ShowStatus,
    SaveKey,
    SaveSteamId
}
