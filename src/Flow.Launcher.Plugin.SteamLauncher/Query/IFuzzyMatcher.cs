using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

/// <summary>
/// Abstraction over Flow Launcher's <c>IPublicAPI.FuzzySearch</c>. Lets services
/// rank and highlight matches without taking a hard dependency on the host API,
/// and lets tests provide a deterministic fake.
/// </summary>
public interface IFuzzyMatcher
{
    MatchResult Match(string query, string text);
}
