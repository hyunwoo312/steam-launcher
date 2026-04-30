namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public interface IRegistryWriter
{
    /// <summary>
    /// Writes a REG_SZ value under HKCU\<paramref name="keyPath"/>. Creates the key
    /// if missing. Throws on access-denied or other write failure — callers should
    /// treat those as user-actionable errors.
    /// </summary>
    void WriteCurrentUserString(string keyPath, string valueName, string value);

    /// <summary>
    /// Writes a REG_DWORD value under HKCU\<paramref name="keyPath"/>. Creates the key
    /// if missing. Throws on access-denied or other write failure.
    /// </summary>
    void WriteCurrentUserDword(string keyPath, string valueName, int value);
}
