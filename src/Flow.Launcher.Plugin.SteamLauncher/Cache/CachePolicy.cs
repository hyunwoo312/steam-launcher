namespace Flow.Launcher.Plugin.SteamLauncher.Cache;

public sealed record CachePolicy(string Domain, TimeSpan SuccessTtl, TimeSpan FailureTtl);
