using Microsoft.Win32;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class RegistryReader : IRegistryReader
{
    public string? ReadCurrentUserString(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }
}
