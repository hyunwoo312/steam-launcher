using System.Globalization;
using System.IO;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class AccountSwitcherService(
    ISteamPathResolver pathResolver,
    IVdfParser parser,
    IVdfWriter writer,
    IRegistryWriter registryWriter,
    IRegistryReader registryReader,
    ISteamProcessController processController,
    Action<string, string, Exception>? logException = null) : IAccountSwitcherService
{
    private const string SteamRegistryKey = @"Software\Valve\Steam";
    private const string AutoLoginUserValue = "AutoLoginUser";
    private const string RememberPasswordValue = "RememberPassword";

    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(4);

    public IReadOnlyList<KnownAccount> GetKnownAccounts()
    {
        IReadOnlyList<KnownAccount> empty = [];

        string installPath;
        try { installPath = pathResolver.GetSteamInstallPath(); }
        catch (SteamPathNotFoundException) { return empty; }

        var path = Path.Combine(installPath, "config", "loginusers.vdf");
        if (!File.Exists(path)) return empty;

        VdfNode.Object doc;
        try { doc = parser.ParseFile(path); }
        catch (VdfParseException) { return empty; }

        var users = doc.GetObject("users");
        if (users is null) return empty;

        var accounts = new List<KnownAccount>();
        foreach (var (key, value) in users.Children)
        {
            if (!ulong.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId64)) continue;
            if (value is not VdfNode.Object obj) continue;

            var accountName = obj.GetString("AccountName");
            if (string.IsNullOrEmpty(accountName)) continue;

            var personaName = obj.GetString("PersonaName") ?? accountName;
            var isCurrent = obj.GetString("MostRecent") == "1";
            var rememberPassword = obj.GetString("RememberPassword") == "1";
            var timestamp = long.TryParse(
                obj.GetString("Timestamp"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var ts)
                    ? ts
                    : (long?)null;

            var avatarFile = Path.Combine(installPath, "config", "avatarcache", $"{steamId64}.png");
            var avatarPath = File.Exists(avatarFile) ? avatarFile : null;

            accounts.Add(new KnownAccount
            {
                SteamId64 = steamId64,
                AccountName = accountName,
                PersonaName = personaName,
                LastLoginUnix = timestamp,
                IsCurrent = isCurrent,
                RememberPassword = rememberPassword,
                AvatarPath = avatarPath
            });
        }

        return accounts
            .OrderByDescending(a => a.LastLoginUnix ?? 0L)
            .ToList();
    }

    public async Task<SwitchResult> SwitchToAsync(ulong steamId64, CancellationToken ct)
    {
        string installPath;
        try { installPath = pathResolver.GetSteamInstallPath(); }
        catch (SteamPathNotFoundException) { return SwitchResult.SteamNotInstalled(); }

        var loginUsersPath = Path.Combine(installPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath)) return SwitchResult.SteamNotInstalled();

        var accounts = GetKnownAccounts();
        var target = accounts.FirstOrDefault(a => a.SteamId64 == steamId64);
        if (target is null) return SwitchResult.AccountNotFound(steamId64);

        if (processController.IsGameRunning()) return SwitchResult.GameRunning();

        try
        {
            await Task.Run(() => processController.KillAllSteamProcesses(KillTimeout), ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return SwitchResult.KillFailed();
        }

        try
        {
            await Task.Run(() => RewriteLoginUsers(loginUsersPath, steamId64), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logException?.Invoke(nameof(AccountSwitcherService),
                $"Failed to rewrite loginusers.vdf for steamId64={steamId64}", ex);
            return SwitchResult.LoginUsersWriteFailed();
        }

        try
        {
            registryWriter.WriteCurrentUserString(SteamRegistryKey, AutoLoginUserValue, target.AccountName);
            registryWriter.WriteCurrentUserDword(SteamRegistryKey, RememberPasswordValue, 1);

            // Read back: UAC virtualization can silently swallow registry writes.
            var written = registryReader.ReadCurrentUserString(SteamRegistryKey, AutoLoginUserValue);
            if (!string.Equals(written, target.AccountName, StringComparison.Ordinal))
            {
                logException?.Invoke(nameof(AccountSwitcherService),
                    $"AutoLoginUser readback mismatch: wrote \"{target.AccountName}\", read \"{written ?? "<null>"}\"",
                    new InvalidOperationException("Registry readback mismatch"));
                return SwitchResult.RegistryWriteFailed();
            }
        }
        catch (Exception)
        {
            return SwitchResult.RegistryWriteFailed();
        }

        await Task.Delay(SettleDelay, ct).ConfigureAwait(false);

        processController.LaunchSteamCold();

        return SwitchResult.Ok(target.AccountName);
    }

    private void RewriteLoginUsers(string loginUsersPath, ulong targetSteamId64)
    {
        var doc = parser.ParseFile(loginUsersPath);
        var users = doc.GetObject("users")
                    ?? throw new InvalidOperationException("loginusers.vdf has no \"users\" object");

        var targetKey = targetSteamId64.ToString(CultureInfo.InvariantCulture);
        if (!users.Children.ContainsKey(targetKey))
            throw new InvalidOperationException($"steamId64 {targetSteamId64} not present in loginusers.vdf");

        var newUsers = new Dictionary<string, VdfNode>(users.Children, StringComparer.Ordinal);
        foreach (var (key, value) in users.Children)
        {
            if (value is not VdfNode.Object userObj) continue;

            var isTarget = key == targetKey;
            var children = new Dictionary<string, VdfNode>(userObj.Children, StringComparer.Ordinal);
            children["MostRecent"] = new VdfNode.String(isTarget ? "1" : "0");
            if (isTarget)
            {
                children["AllowAutoLogin"]   = new VdfNode.String("1");
                children["RememberPassword"] = new VdfNode.String("1");
            }
            newUsers[key] = new VdfNode.Object(children);
        }

        var newRoot = new VdfNode.Object(
            new Dictionary<string, VdfNode>(doc.Children, StringComparer.Ordinal)
            {
                ["users"] = new VdfNode.Object(newUsers)
            });

        var rendered = writer.Write(newRoot);

        var backupPath = loginUsersPath + "_last";
        var tempPath = loginUsersPath + ".tmp";

        if (File.Exists(loginUsersPath))
            File.Copy(loginUsersPath, backupPath, overwrite: true);

        File.WriteAllText(tempPath, rendered);
        File.Move(tempPath, loginUsersPath, overwrite: true);
    }
}
