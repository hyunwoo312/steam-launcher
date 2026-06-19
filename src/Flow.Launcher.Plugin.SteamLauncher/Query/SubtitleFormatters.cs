using Flow.Launcher.Plugin.SteamLauncher.Models;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal static class SubtitleFormatters
{
    public static string Playtime(long? totalMinutes, long? recentMinutes)
    {
        if (totalMinutes is null or 0) return "Never played";
        var total = HoursLabel(totalMinutes.Value, includePlayedSuffix: true);
        return recentMinutes is > 0
            ? $"{total} ({HoursLabel(recentMinutes.Value, includePlayedSuffix: false)} last 2wk)"
            : total;
    }

    public static string LastPlayedRelative(DateTimeOffset lastPlayed)
    {
        var ago = DateTimeOffset.UtcNow - lastPlayed;
        return ago.TotalDays switch
        {
            < 1 => "today",
            < 2 => "yesterday",
            < 30 => $"{(int)ago.TotalDays} days ago",
            < 365 => $"{(int)(ago.TotalDays / 30)} months ago",
            _ => $"{(int)(ago.TotalDays / 365)} years ago"
        };
    }

    public static string LastPlayedForOwnedRow(DateTimeOffset? lastPlayed) =>
        lastPlayed is null ? "Last played never" : $"Last played {LastPlayedRelative(lastPlayed.Value)}";

    public static string Bytes(long bytes)
    {
        if (bytes <= 0) return "size unknown";
        const long gb = 1024L * 1024 * 1024;
        const long mb = 1024L * 1024;
        if (bytes >= gb) return $"{bytes / (double)gb:F1} GB";
        if (bytes >= mb) return $"{bytes / (double)mb:F0} MB";
        return $"{bytes / 1024.0:F0} KB";
    }

    public static string? Reviews(GameMetadata meta)
    {
        if (string.IsNullOrEmpty(meta.ReviewSummary)) return null;
        if (meta.ReviewCount is null or <= 0) return meta.ReviewSummary;
        return $"{meta.ReviewSummary} ({Count(meta.ReviewCount.Value)})";
    }

    public static bool IsFree(decimal? price) => price is null or 0m;

    public static string Price(decimal? amount, string? currency)
    {
        if (IsFree(amount)) return "Free";

        var value = amount!.Value;
        return currency?.ToUpperInvariant() switch
        {
            null or "" or "USD" => $"${value:F2}",
            "EUR" => $"€{value:F2}",
            "GBP" => $"£{value:F2}",
            "JPY" or "CNY" => $"¥{value:F2}",
            "KRW" => $"₩{value:F2}",
            "RUB" => $"₽{value:F2}",
            "INR" => $"₹{value:F2}",
            "BRL" => $"R${value:F2}",
            "CAD" => $"CA${value:F2}",
            "AUD" => $"A${value:F2}",
            "MXN" => $"MX${value:F2}",
            var code => $"{value:F2} {code}"
        };
    }

    private static string HoursLabel(long minutes, bool includePlayedSuffix)
    {
        var suffix = includePlayedSuffix ? " played" : string.Empty;
        if (minutes < 60) return $"{minutes} min{suffix}";
        var hours = minutes / 60.0;
        return hours < 10 ? $"{hours:F1} hrs{suffix}" : $"{minutes / 60} hrs{suffix}";
    }

    private static string Count(int n) =>
        n >= 1_000_000 ? $"{n / 1_000_000.0:F1}M"
        : n >= 1_000   ? $"{n / 1_000.0:F1}K"
        : n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
