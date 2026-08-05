namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public static class InstalledGameExtensions
{
    // Steam appmanifest StateFlags bitfield (multiple bits can be set).
    private const uint StateUninstalled = 1;
    private const uint StateUpdateRequired = 2;
    private const uint StateFilesMissing = 32;
    private const uint StateUpdateRunning = 256;
    private const uint StateUpdatePaused = 512;
    private const uint StateUpdateStarted = 1024;
    private const uint StateUninstalling = 2048;

    /// <summary>
    /// Classifies a manifest's StateFlags, most-blocking state first: a game being removed
    /// or missing its files cannot be played, so those outrank any pending update.
    /// </summary>
    public static InstallState GetInstallState(this InstalledGame game)
    {
        var flags = game.StateFlags;
        if ((flags & StateUninstalling) != 0) return InstallState.Uninstalling;
        if ((flags & StateFilesMissing) != 0) return InstallState.FilesMissing;
        if ((flags & StateUpdateRunning) != 0) return InstallState.Updating;
        if ((flags & StateUpdateStarted) != 0) return InstallState.Updating;
        if ((flags & StateUpdatePaused) != 0) return InstallState.UpdatePaused;
        if ((flags & StateUpdateRequired) != 0) return InstallState.UpdateRequired;
        return InstallState.Installed;
    }

    /// <summary>
    /// True when Steam says the app is not on disk at all. Such manifests linger after an
    /// uninstall and must not be listed as installed games.
    /// </summary>
    public static bool IsAbsentFromDisk(this InstalledGame game) =>
        (game.StateFlags & StateUninstalled) != 0;
}
