using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Settings;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal sealed class ApiConfigRowBuilder(
    IApiKeyStore apiKeyStore,
    PluginSettings settings,
    Action saveSettings,
    Action invalidateUserCaches,
    Func<ulong?> getActiveSteamId,
    Action<string, bool>? changeQuery,
    Action<string, string>? showToast,
    string actionKeyword,
    string defaultIconPath,
    Action<string, string, Exception> logException)
{
    public List<Result> Build(ParsedQuery.ApiConfig api) => api.Action switch
    {
        ApiConfigAction.ShowStatus => BuildShowStatus(),
        ApiConfigAction.SaveKey => BuildSaveKey(api.Argument),
        ApiConfigAction.SaveSteamId => BuildSaveSteamId(api.Argument),
        _ => []
    };

    private List<Result> BuildShowStatus()
    {
        if (!apiKeyStore.IsConfigured)
            return [BuildGetKeyRow(), BuildPasteKeyRow()];

        if (string.IsNullOrEmpty(settings.SteamId64))
            return [BuildSetSteamIdRow(), BuildRemoveKeyRow()];

        return [BuildConfiguredRow(), BuildRemoveKeyRow()];
    }

    private Result BuildRemoveKeyRow() => new()
    {
        Title = "Remove API key",
        SubTitle = "Deletes the encrypted key file — disables st me / st new / st friends / st multi",
        IcoPath = defaultIconPath,
        Action = _ =>
        {
            try
            {
                apiKeyStore.Save(null);
                invalidateUserCaches();
                showToast?.Invoke("API key removed", "Encrypted key file deleted");
            }
            catch (Exception ex) { logException(nameof(ApiConfigRowBuilder), "Remove API key failed", ex); }
            changeQuery?.Invoke($"{actionKeyword} api", true);
            return false;
        }
    };

    private Result BuildGetKeyRow() => new()
    {
        Title = "Step 1 of 2 — Get your Steam Web API key",
        SubTitle = "Opens browser to steamcommunity.com/dev/apikey",
        IcoPath = defaultIconPath,
        Action = _ =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://steamcommunity.com/dev/apikey",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logException(nameof(ApiConfigRowBuilder), "Open API key page failed", ex);
            }
            return true;
        }
    };

    private Result BuildPasteKeyRow() => new()
    {
        Title = "Step 2 of 2 — Save your API key",
        SubTitle = $"Press Enter, paste your key after `{actionKeyword} api `, then Enter again to save",
        IcoPath = defaultIconPath,
        Action = _ =>
        {
            changeQuery?.Invoke($"{actionKeyword} api ", false);
            return false;
        }
    };

    private Result BuildSetSteamIdRow()
    {
        var detected = getActiveSteamId();
        if (detected is { } id)
        {
            var idText = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new()
            {
                Title = "Step 2 of 2 — Set your Steam ID",
                SubTitle = $"Detected as {idText} · press Enter to save",
                IcoPath = defaultIconPath,
                Action = _ =>
                {
                    changeQuery?.Invoke($"{actionKeyword} api id {idText}", true);
                    return false;
                }
            };
        }

        return new()
        {
            Title = "Step 2 of 2 — Set your Steam ID",
            SubTitle = $"Run `{actionKeyword} api id <17-digit-steamid64>`",
            IcoPath = defaultIconPath
        };
    }

    private Result BuildConfiguredRow()
    {
        var key = apiKeyStore.Load();
        var maskedKey = MaskKey(key);
        return new()
        {
            Title = "API configured ✓",
            SubTitle = $"key {maskedKey} · ID {settings.SteamId64} · replace via `{actionKeyword} api <new-key>` or `{actionKeyword} api id <new-id>`",
            IcoPath = defaultIconPath
        };
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (key.Length < 8) return key;
        return $"{key[..4]}…{key[^4..]}";
    }

    private List<Result> BuildSaveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return [Hint("Usage: `st api <key>`", "Paste your 32-character Steam Web API key")];

        return
        [
            new()
            {
                Title = "Save API key",
                SubTitle = $"Encrypts and stores `{key[..Math.Min(8, key.Length)]}…` (DPAPI, current-user)",
                IcoPath = defaultIconPath,
                Action = _ =>
                {
                    try
                    {
                        apiKeyStore.Save(key);
                        invalidateUserCaches();
                        showToast?.Invoke("API key saved", "Stored encrypted with DPAPI (current-user scope)");
                    }
                    catch (Exception ex) { logException(nameof(ApiConfigRowBuilder), "Save API key failed", ex); }
                    changeQuery?.Invoke($"{actionKeyword} api", true);
                    return false;
                }
            }
        ];
    }

    private List<Result> BuildSaveSteamId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ulong.TryParse(id, out _))
            return [Hint("Usage: `st api id <steamid64>`", "17-digit numeric Steam ID")];

        return
        [
            new()
            {
                Title = $"Save Steam ID `{id}`",
                SubTitle = "Stored in plaintext settings (Steam IDs are public)",
                IcoPath = defaultIconPath,
                Action = _ =>
                {
                    settings.SteamId64 = id;
                    try
                    {
                        saveSettings();
                        invalidateUserCaches();
                        showToast?.Invoke("Steam ID saved", $"Active user is now {id}");
                    }
                    catch (Exception ex) { logException(nameof(ApiConfigRowBuilder), "Save Steam ID failed", ex); }
                    changeQuery?.Invoke($"{actionKeyword} api", true);
                    return false;
                }
            }
        ];
    }

    private Result Hint(string title, string subtitle) => new()
    {
        Title = title,
        SubTitle = subtitle,
        IcoPath = defaultIconPath
    };
}
