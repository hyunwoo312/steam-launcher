namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public static class CachePolicies
{
    public const string Search = "search";
    public const string PlayerCount = "player_count";
    public const string ReviewScore = "review_score";
    public const string AppDetails = "app_details";
    public const string OwnedGames = "owned_games";
    public const string FriendList = "friend_list";
    public const string PlayerSummaries = "player_summaries";

    public static IReadOnlyList<CachePolicy> Default { get; } =
    [
        new(Search,           TimeSpan.FromSeconds(30),  TimeSpan.FromSeconds(30)),
        new(PlayerCount,      TimeSpan.FromMinutes(4),   TimeSpan.FromMinutes(30)),
        new(ReviewScore,      TimeSpan.FromHours(4),     TimeSpan.FromHours(1)),
        new(AppDetails,       TimeSpan.FromDays(30),     TimeSpan.FromHours(6)),
        new(OwnedGames,       TimeSpan.FromHours(24),    TimeSpan.FromHours(1)),
        new(FriendList,       TimeSpan.FromMinutes(15),  TimeSpan.FromMinutes(5)),
        new(PlayerSummaries,  TimeSpan.FromMinutes(1),   TimeSpan.FromSeconds(30))
    ];
}
