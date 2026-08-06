namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public abstract record ParsedQuery
{
    public sealed record Empty : ParsedQuery;
    public sealed record LibraryFilter(string Term) : ParsedQuery;
    public sealed record Me : ParsedQuery;
    public sealed record RecentlyAddedNeverPlayed : ParsedQuery;

    public sealed record ApiConfig(ApiConfigAction Action, string? Argument) : ParsedQuery;

    public sealed record FriendsList(string? Filter) : ParsedQuery;

    public sealed record StatusSwitcher : ParsedQuery;
    public sealed record AccountSwitcher(string? ConfirmAccountName = null) : ParsedQuery;
    public sealed record MultiplayerWith(string FriendName) : ParsedQuery;

    public sealed record VerifyGame(string? Filter) : ParsedQuery;
    public sealed record UninstallGame(string? Filter) : ParsedQuery;
    public sealed record OpenSteamWindow(SteamWindow Window) : ParsedQuery;
}

public enum ApiConfigAction
{
    ShowStatus,
    SaveKey,
    SaveSteamId
}

/// <summary>A Steam client window reachable by a bare <c>steam://open/…</c> URI.</summary>
public enum SteamWindow
{
    Settings,
    Downloads,
    BigPicture,
    Screenshots,
    Redeem
}
