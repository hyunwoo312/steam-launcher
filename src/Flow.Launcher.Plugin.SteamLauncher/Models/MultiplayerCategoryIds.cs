namespace Flow.Launcher.Plugin.SteamLauncher.Models;

/// <summary>
/// Steam app-detail category IDs that mark a title as having some flavour of
/// multiplayer/co-op. Sourced from the public storefront app-details response.
/// Used by <c>MultiplayerService</c> to filter the shared-games intersection
/// down to titles two players can actually play together.
/// </summary>
public static class MultiplayerCategoryIds
{
    public const int MultiPlayer = 1;
    public const int CoOp = 9;
    public const int Mmo = 20;
    public const int CrossPlatformMultiplayer = 27;
    public const int OnlinePvP = 36;
    public const int SharedSplitScreenPvP = 37;
    public const int OnlineCoOp = 38;
    public const int SharedSplitScreenCoOp = 39;
    public const int LanPvP = 47;
    public const int LanCoOp = 48;
    public const int SharedSplitScreen = 49;

    public static IReadOnlySet<int> All { get; } = new HashSet<int>
    {
        MultiPlayer,
        CoOp,
        Mmo,
        CrossPlatformMultiplayer,
        OnlinePvP,
        SharedSplitScreenPvP,
        OnlineCoOp,
        SharedSplitScreenCoOp,
        LanPvP,
        LanCoOp,
        SharedSplitScreen
    };
}
