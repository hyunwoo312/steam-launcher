namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public interface ISteamProcessController
{
    /// <summary>
    /// Force-kills the four Steam-related processes in order: <c>steam.exe</c> (with
    /// process-tree), <c>steamwebhelper.exe</c>, <c>GameOverlayUI.exe</c>, <c>steamservice.exe</c>.
    /// Waits up to <paramref name="timeout"/> for all four to exit fully. Throws if any
    /// remain running after the deadline.
    /// </summary>
    void KillAllSteamProcesses(TimeSpan timeout);

    /// <summary>
    /// Cold-start launch: tries the <c>steam://nav/games</c> URI first (Steam handles
    /// the cold-start via the registered URI handler). Falls back to direct exe launch
    /// if the URI handler fails.
    /// </summary>
    void LaunchSteamCold();

    /// <summary>
    /// True if Steam appears to be running a game or download — i.e. there is at least
    /// one process whose parent is <c>steam.exe</c> AND whose name is NOT in the
    /// always-running internal-helpers allowlist (steamwebhelper / steamservice / GameOverlayUI).
    /// Used as a guard before killing Steam mid-session.
    /// </summary>
    bool IsGameRunning();

    /// <summary>
    /// True if Steam's Big Picture window is currently open. Detected by looking for a
    /// visible <c>SDL_app</c> window whose title contains "Big Picture": Steam sets no
    /// registry marker for the mode, and its Big Picture and desktop UIs share both a
    /// process and a window class, so the title is the only available discriminator.
    /// Steam's other windows ("Steam Settings", "Friends List") share that class too,
    /// which is why the title must be matched rather than the class alone.
    /// </summary>
    bool IsBigPictureRunning();
}
