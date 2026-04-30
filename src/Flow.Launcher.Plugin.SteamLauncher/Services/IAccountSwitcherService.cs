using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IAccountSwitcherService
{
    /// <summary>
    /// Reads <c>config/loginusers.vdf</c> and returns the registered accounts in
    /// most-recent-login-first order. Empty when Steam isn't installed or the file
    /// is missing or malformed.
    /// </summary>
    IReadOnlyList<KnownAccount> GetKnownAccounts();

    /// <summary>
    /// Kills the four Steam processes, flips <c>MostRecent</c> + auto-login flags in
    /// <c>config/loginusers.vdf</c> for the target SteamID64, writes the matching registry
    /// hints, waits a settle interval, and re-launches Steam. Steam auto-logs in to the
    /// chosen account (provided <c>RememberPassword</c> is set in the file).
    /// </summary>
    Task<SwitchResult> SwitchToAsync(ulong steamId64, CancellationToken ct);
}

public sealed record SwitchResult(SwitchOutcome Status, string? AccountName)
{
    public static SwitchResult Ok(string accountName) => new(SwitchOutcome.Ok, accountName);
    public static SwitchResult SteamNotInstalled() => new(SwitchOutcome.SteamNotInstalled, null);
    public static SwitchResult AccountNotFound(ulong steamId64) =>
        new(SwitchOutcome.AccountNotFound, steamId64.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static SwitchResult KillFailed() => new(SwitchOutcome.KillFailed, null);
    public static SwitchResult LoginUsersWriteFailed() => new(SwitchOutcome.LoginUsersWriteFailed, null);
    public static SwitchResult RegistryWriteFailed() => new(SwitchOutcome.RegistryWriteFailed, null);
    public static SwitchResult GameRunning() => new(SwitchOutcome.GameRunning, null);
}

public enum SwitchOutcome
{
    Ok,
    SteamNotInstalled,
    AccountNotFound,
    KillFailed,
    LoginUsersWriteFailed,
    RegistryWriteFailed,
    GameRunning
}
