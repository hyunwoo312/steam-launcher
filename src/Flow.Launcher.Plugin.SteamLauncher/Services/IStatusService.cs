using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IStatusService
{
    /// <summary>
    /// Launches the Steam URI corresponding to the requested persona state. Steam handles
    /// the change instantly while running. Throws if the launch itself fails.
    /// </summary>
    void Set(PersonaStateDesired desired);
}
