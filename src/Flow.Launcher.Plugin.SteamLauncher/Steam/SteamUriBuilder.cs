namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public static class SteamUriBuilder
{
    public static string RunGame(uint appId) => $"steam://rungameid/{appId}";

    public static string MainWindow() => "steam://open/main";
    public static string LibraryHome() => "steam://nav/games";

    public static string Install(uint appId) => $"steam://install/{appId}";
    public static string Uninstall(uint appId) => $"steam://uninstall/{appId}";
    public static string Validate(uint appId) => $"steam://validate/{appId}";
    public static string Store(uint appId) => $"steam://store/{appId}";
    public static string LibraryDetails(uint appId) => $"steam://nav/games/details/{appId}";
    public static string GameProperties(uint appId) => $"steam://gameproperties/{appId}";

    public static string OpenSettings() => "steam://open/settings";
    public static string OpenDownloads() => "steam://open/downloads";
    public static string OpenFriends() => "steam://open/friends";
    public static string OpenBigPicture() => "steam://open/bigpicture";
    public static string CloseBigPicture() => "steam://close/bigpicture";
    public static string OpenScreenshots() => "steam://open/screenshots";
    public static string OpenRedeem() => "steam://open/activate";

    public static string FriendsMessage(ulong steamId64) => $"steam://friends/message/{steamId64}";
    public static string FriendsJoinGame(ulong steamId64) => $"steam://friends/joingame/{steamId64}";
    public static string Profile(ulong steamId64) => $"steam://url/SteamIDPage/{steamId64}";

    public static string FriendsStatusOnline()    => "steam://friends/status/online";
    public static string FriendsStatusOffline()   => "steam://friends/status/offline";
    public static string FriendsStatusInvisible() => "steam://friends/status/invisible";
}
