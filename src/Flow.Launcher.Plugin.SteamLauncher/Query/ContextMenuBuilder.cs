using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

internal sealed class ContextMenuBuilder
{
    private const string IconFont = "Segoe Fluent Icons";

    private const string IconPlay             = ""; // Play
    private const string IconShop             = ""; // Shop
    private const string IconReading          = ""; // Reading
    private const string IconComment          = ""; // Comment
    private const string IconSettings         = ""; // Settings
    private const string IconFolderOpen       = ""; // FolderOpen
    private const string IconAdd              = ""; // Add
    private const string IconPeopleAdd        = ""; // PeopleAdd
    private const string IconContact          = ""; // Contact
    private const string IconFavoriteStar     = ""; // FavoriteStar
    private const string IconFavoriteStarFill = ""; // FavoriteStarFill
    private const string IconDelete           = ""; // Delete

    private readonly IPublicAPI _api;
    private readonly PluginSettings _settings;
    private readonly Action _saveSettings;
    private readonly string _defaultIconPath;
    private readonly Action<string, string, Exception> _logException;

    public ContextMenuBuilder(
        IPublicAPI api,
        PluginSettings settings,
        Action saveSettings,
        string defaultIconPath,
        Action<string, string, Exception> logException)
    {
        _api = api;
        _settings = settings;
        _saveSettings = saveSettings;
        _defaultIconPath = defaultIconPath;
        _logException = logException;
    }

    public List<Result> Build(ContextData data) => data switch
    {
        ContextData.Game g => BuildGameMenu(g),
        ContextData.Friend f => BuildFriendMenu(f),
        _ => []
    };

    private List<Result> BuildGameMenu(ContextData.Game g)
    {
        var results = new List<Result>
        {
            UriRow("Launch game",                IconPlay,     SteamUriBuilder.RunGame(g.AppId),                                        "Launch via Steam"),
            UriRow("Open store page",            IconShop,     $"steam://store/{g.AppId}",                                              null),
            UriRow("Open community guides",      IconReading,  SteamOverlay($"https://steamcommunity.com/app/{g.AppId}/guides/"),       null),
            UriRow("Open community discussions", IconComment,  SteamOverlay($"https://steamcommunity.com/app/{g.AppId}/discussions/"),  null),
            UriRow("Open game properties",       IconSettings, SteamUriBuilder.GameProperties(g.AppId),                                 null)
        };

        if (!string.IsNullOrEmpty(g.InstallPath))
        {
            var installPath = g.InstallPath;
            results.Add(SteamUriRow("Uninstall", IconDelete, SteamUriBuilder.Uninstall(g.AppId), "Open Steam's uninstall confirmation"));
            results.Add(new Result
            {
                Title = "Open game folder",
                SubTitle = installPath,
                IcoPath = _defaultIconPath,
                Glyph = Glyph(IconFolderOpen),
                Action = _ =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{installPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logException(nameof(ContextMenuBuilder), $"Open folder failed: {installPath}", ex);
                    }
                    return true;
                }
            });
        }

        return results;
    }

    private List<Result> BuildFriendMenu(ContextData.Friend f)
    {
        var dmUri = SteamUriBuilder.FriendsMessage(f.SteamId64);
        var joinUri = SteamUriBuilder.FriendsJoinGame(f.SteamId64);
        var profileUri = SteamUriBuilder.Profile(f.SteamId64);

        var rows = new List<Result>
        {
            SteamUriRow("DM",             IconComment, dmUri,      "Open chat"),
            SteamUriRow("Invite to game", IconAdd,     dmUri,      "Open chat — Steam shows the Invite button there"),
            new()
            {
                Title = "Join game",
                SubTitle = f.IsInGame ? "Join their lobby" : "Friend is not in a joinable game",
                IcoPath = _defaultIconPath,
                Glyph = Glyph(IconPeopleAdd),
                Action = _ =>
                {
                    if (!f.IsInGame)
                    {
                        _logException(nameof(ContextMenuBuilder), $"Join game ignored: {f.PersonaName} not in game", new InvalidOperationException("not in game"));
                        return true;
                    }
                    LaunchSteamUri(joinUri, $"Join game failed: {f.PersonaName}");
                    return true;
                }
            },
            SteamUriRow("Open profile",   IconContact, profileUri, "View Steam profile")
        };

        var isFav = f.IsFavorite;
        rows.Add(new Result
        {
            Title = isFav ? "Unfavorite" : "Favorite",
            SubTitle = isFav ? "Remove from priority list" : "Pin to top of st friends results",
            IcoPath = _defaultIconPath,
            Glyph = Glyph(isFav ? IconFavoriteStarFill : IconFavoriteStar),
            Action = _ =>
            {
                var nowFavorite = ToggleFavorite(f.SteamId64);
                var verb = nowFavorite ? "Favorited" : "Unfavorited";
                var count = _settings.FavoriteFriendIds.Count;
                var noun = count == 1 ? "favorite" : "favorites";
                _api.ShowMsg($"{verb} {f.PersonaName}", $"{count} {noun} total");
                try { _api.ReQuery(true); }
                catch (Exception ex) { _logException(nameof(ContextMenuBuilder), "ReQuery after favorite toggle failed", ex); }
                return false;
            }
        });

        return rows;
    }

    private bool ToggleFavorite(ulong steamId)
    {
        try
        {
            var ids = _settings.FavoriteFriendIds;
            var added = !ids.Remove(steamId);
            if (added)
                ids.Add(steamId);
            _saveSettings();
            return added;
        }
        catch (Exception ex)
        {
            _logException(nameof(ContextMenuBuilder), $"Toggle favorite failed: {steamId}", ex);
            return _settings.FavoriteFriendIds.Contains(steamId);
        }
    }

    private Result SteamUriRow(string title, string codepoint, string uri, string? subtitle) => new()
    {
        Title = title,
        SubTitle = subtitle ?? uri,
        IcoPath = _defaultIconPath,
        Glyph = Glyph(codepoint),
        Action = _ =>
        {
            LaunchSteamUri(uri, $"Open URI failed: {uri}");
            return true;
        }
    };

    private Result UriRow(string title, string codepoint, string uri, string? subtitle) => new()
    {
        Title = title,
        SubTitle = subtitle ?? uri,
        IcoPath = _defaultIconPath,
        Glyph = Glyph(codepoint),
        Action = _ =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logException(nameof(ContextMenuBuilder), $"Open URI failed: {uri}", ex);
            }
            return true;
        }
    };

    private void LaunchSteamUri(string uri, string failureMessage)
    {
        try { SteamProcessRunner.Launch(uri); }
        catch (Exception ex) { _logException(nameof(ContextMenuBuilder), failureMessage, ex); }
    }

    private static GlyphInfo Glyph(string codepoint) => new(IconFont, codepoint);

    // Use steam://openurl/ so links render inside Steam's overlay browser.
    private static string SteamOverlay(string url) => $"steam://openurl/{url}";
}
