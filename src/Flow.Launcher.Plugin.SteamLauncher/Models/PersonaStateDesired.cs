namespace Flow.Launcher.Plugin.SteamLauncher.Models;

/// <summary>
/// User-settable persona states dispatched via <c>steam://friends/status/&lt;name&gt;</c>.
/// Distinct from the read-side <see cref="PersonaState"/> enum because Steam exposes
/// only three writable values; the other PersonaState codes (Busy, Away, Snooze, etc.)
/// are derived by the client, not user-settable here.
/// </summary>
public enum PersonaStateDesired
{
    Offline = 0,
    Online = 1,
    Invisible = 7
}
