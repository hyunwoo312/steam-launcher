namespace Flow.Launcher.Plugin.SteamLauncher.Models;

/// <summary>
/// A row from <c>config/loginusers.vdf</c> as surfaced by the account switcher.
/// <see cref="IsCurrent"/> is true for the row whose <c>MostRecent</c> flag is set —
/// that account is the one Steam will auto-login as on next launch with no overrides.
/// <see cref="RememberPassword"/> mirrors the per-account flag in loginusers.vdf;
/// when false, Steam will prompt for password on switch even with AutoLoginUser set.
/// <see cref="AvatarPath"/> points at <c>config/avatarcache/&lt;SteamID64&gt;.png</c> when
/// Steam has cached one for this account; null when no local file exists.
/// </summary>
public sealed record KnownAccount
{
    public required ulong SteamId64 { get; init; }
    public required string AccountName { get; init; }
    public required string PersonaName { get; init; }
    public long? LastLoginUnix { get; init; }
    public bool IsCurrent { get; init; }
    public bool RememberPassword { get; init; }
    public string? AvatarPath { get; init; }
}
