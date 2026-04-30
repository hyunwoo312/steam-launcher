namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public static class InstalledGameExtensions
{
    // Steam appmanifest StateFlags bitfield (multiple bits can be set).
    private const uint StateUpdateRequired = 2;
    private const uint StateUpdateRunning = 256;
    private const uint StateUpdatePaused = 512;
    private const uint StateUpdateStarted = 1024;

    public static InstallState GetInstallState(this InstalledGame game)
    {
        var flags = game.StateFlags;
        if ((flags & StateUpdateRunning) != 0) return InstallState.Updating;
        if ((flags & StateUpdateStarted) != 0) return InstallState.Updating;
        if ((flags & StateUpdatePaused) != 0) return InstallState.UpdatePaused;
        if ((flags & StateUpdateRequired) != 0) return InstallState.UpdateRequired;
        return InstallState.Installed;
    }
}
