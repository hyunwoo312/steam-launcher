using System.IO;

namespace Flow.Launcher.Plugin.SteamLauncher.Models;

public sealed record InstalledGame
{
    public required uint AppId { get; init; }
    public required string Name { get; init; }
    public required string InstallDir { get; init; }
    public required string LibraryPath { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }
    public long SizeOnDiskBytes { get; init; }
    public uint StateFlags { get; init; }
    public string? IconPath { get; init; }
    public long? PlaytimeMinutes { get; init; }
    public long? PlaytimeLast2WeeksMinutes { get; init; }

    public string FullInstallPath => Path.Combine(LibraryPath, "steamapps", "common", InstallDir);
}
