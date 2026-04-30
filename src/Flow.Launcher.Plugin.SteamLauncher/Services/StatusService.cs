using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public sealed class StatusService(Action<string> launchUri) : IStatusService
{
    public void Set(PersonaStateDesired desired)
    {
        var uri = desired switch
        {
            PersonaStateDesired.Online    => SteamUriBuilder.FriendsStatusOnline(),
            PersonaStateDesired.Offline   => SteamUriBuilder.FriendsStatusOffline(),
            PersonaStateDesired.Invisible => SteamUriBuilder.FriendsStatusInvisible(),
            _ => throw new ArgumentOutOfRangeException(nameof(desired), desired, "Unsupported persona state")
        };
        launchUri(uri);
    }
}
