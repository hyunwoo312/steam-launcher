namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public sealed record GameMetadata
{
    public string? ReviewSummary { get; init; }
    public int? ReviewCount { get; init; }
    public string? ReleaseDate { get; init; }
    public string? Developer { get; init; }
    public IReadOnlyList<int> CategoryIds { get; init; } = Array.Empty<int>();

    public static readonly GameMetadata Empty = new();
}
