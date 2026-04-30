using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Services;

public interface IMultiplayerService
{
    Task<MultiplayerResult> FindSharedAsync(string friendNameQuery, CancellationToken ct);
}

public sealed record MultiplayerResult(
    MultiplayerOutcome Outcome,
    string? PersonaName,
    ulong? FriendSteamId,
    IReadOnlyList<SharedMultiplayerGame> SharedGames,
    Friend? Friend)
{
    public static MultiplayerResult NoFriendMatch() =>
        new(MultiplayerOutcome.NoFriendMatch, null, null, [], null);

    public static MultiplayerResult PrivateOrEmpty(string personaName, ulong steamId, Friend friend) =>
        new(MultiplayerOutcome.PrivateOrEmpty, personaName, steamId, [], friend);

    public static MultiplayerResult Match(
        string personaName,
        ulong steamId,
        IReadOnlyList<SharedMultiplayerGame> games,
        Friend friend) =>
        new(MultiplayerOutcome.Match, personaName, steamId, games, friend);
}

public enum MultiplayerOutcome
{
    NoFriendMatch,
    PrivateOrEmpty,
    Match
}

public sealed record SharedMultiplayerGame(
    uint AppId,
    string Name,
    long MyPlaytimeMinutes,
    long FriendPlaytimeMinutes,
    DateTimeOffset? MyLastPlayed,
    DateTimeOffset? FriendLastPlayed);
