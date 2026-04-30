namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public sealed record Friend
{
    public required ulong SteamId64 { get; init; }
    public required string PersonaName { get; init; }
    public string? AvatarUrl { get; init; }
    public PersonaState PersonaState { get; init; }
    public uint? CurrentGameAppId { get; init; }
    public string? CurrentGameName { get; init; }
    public long? LastLogoffUnix { get; init; }

    public bool IsInGame => CurrentGameAppId is not null;
}
