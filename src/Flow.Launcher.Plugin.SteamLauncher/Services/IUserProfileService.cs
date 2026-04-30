using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IUserProfileService
{
    /// <summary>
    /// Builds a `UserProfile` summary from owned games + level + recent activity.
    /// Returns null if Steam ID isn't configured.
    /// </summary>
    Task<UserProfile?> GetMyProfileAsync(CancellationToken ct);
}
