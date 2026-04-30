using Microsoft.Win32;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class RegistryWriter : IRegistryWriter
{
    public void WriteCurrentUserString(string keyPath, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
                        ?? throw new InvalidOperationException(
                            $"Could not open or create HKCU\\{keyPath}");
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void WriteCurrentUserDword(string keyPath, string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
                        ?? throw new InvalidOperationException(
                            $"Could not open or create HKCU\\{keyPath}");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
