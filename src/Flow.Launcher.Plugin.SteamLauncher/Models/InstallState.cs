namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public enum InstallState
{
    Installed,
    UpdateRequired,
    Updating,
    UpdatePaused,

    /// <summary>Steam reports files missing from disk; launching will trigger a repair.</summary>
    FilesMissing,

    /// <summary>Steam is removing this game; the manifest is still on disk but the install is going away.</summary>
    Uninstalling
}
