namespace Flow.Launcher.Plugin.SteamLauncher.Security;

public interface IApiKeyStore
{
    /// <summary>
    /// Returns the stored API key, or null if none has been saved.
    /// Throws on unexpected I/O or decryption failures.
    /// </summary>
    string? Load();

    /// <summary>
    /// Stores the API key, encrypted at rest with DPAPI bound to the current user.
    /// Passing null or whitespace clears any stored key.
    /// </summary>
    void Save(string? apiKey);

    /// <summary>True if a non-empty key is currently stored.</summary>
    bool IsConfigured { get; }
}
