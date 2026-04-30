namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public abstract record ContextData
{
    public sealed record Game(uint AppId, string GameName, string? InstallPath) : ContextData;
    public sealed record Friend(ulong SteamId64, string PersonaName, bool IsFavorite, bool IsInGame, uint? CurrentGameAppId) : ContextData;
}
