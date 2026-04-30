using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

/// <summary>
/// Production <see cref="IFuzzyMatcher"/> that delegates to Flow's host API.
/// </summary>
public sealed class FlowFuzzyMatcher(IPublicAPI api) : IFuzzyMatcher
{
    public MatchResult Match(string query, string text) => api.FuzzySearch(query, text);
}
