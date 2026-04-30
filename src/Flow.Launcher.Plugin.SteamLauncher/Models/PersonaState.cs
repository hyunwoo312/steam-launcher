namespace Flow.Launcher.Plugin.SteamLauncher.Models;

/// <summary>
/// Maps the Steam Web API <c>personastate</c> integer to a typed enum.
/// Values come straight from <c>GetPlayerSummaries</c>; <c>InGame</c> is a
/// derived state (<c>personastate=1</c> with a populated <c>gameid</c>),
/// not an API value. The dispatcher / formatter check <c>CurrentGameAppId</c>
/// to promote Online to InGame at render time.
/// </summary>
public enum PersonaState
{
    Offline = 0,
    Online = 1,
    Busy = 2,
    Away = 3,
    Snooze = 4,
    LookingToTrade = 5,
    LookingToPlay = 6
}
