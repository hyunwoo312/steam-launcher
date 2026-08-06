using System.Net.NetworkInformation;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;

namespace Flow.Launcher.Plugin.SteamLauncher.Query;

public sealed class QueryDispatcher
{
    private const int CombinedResultCap = 10;
    private const int NewResultCap = 20;
    private const int MetadataEnrichmentCap = 30;

    private readonly ILocalLibraryService _localLibrary;
    private readonly IFuzzyMatcher _fuzzyMatcher;
    private readonly IStoreSearchService _storeSearch;
    private readonly IOwnedGamesService _ownedGames;
    private readonly IUserProfileService _userProfile;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly PluginSettings _settings;
    private readonly IGameIconResolver _iconResolver;
    private readonly IGameMetadataService _metadata;
    private readonly IFriendsService _friends;
    private readonly IAvatarCache _avatarCache;
    private readonly IStatusService _statusService;
    private readonly IAccountSwitcherService _accountSwitcherService;
    private readonly IMultiplayerService _multiplayerService;
    private readonly Action<string, string>? _showToast;
    private readonly Action<string, bool>? _changeQuery;
    private readonly string _actionKeyword;
    private readonly string? _localPersonaName;
    private readonly string _defaultIconPath;
    private readonly Action<string, string, Exception> _logException;
    private readonly Func<bool> _isNetworkAvailable;
    private readonly Func<bool>? _isBigPictureRunning;
    private readonly ApiConfigRowBuilder _apiConfigRows;

    public QueryDispatcher(
        ILocalLibraryService localLibrary,
        IFuzzyMatcher fuzzyMatcher,
        IStoreSearchService storeSearch,
        IOwnedGamesService ownedGames,
        IUserProfileService userProfile,
        IApiKeyStore apiKeyStore,
        PluginSettings settings,
        Action saveSettings,
        IGameIconResolver iconResolver,
        IGameMetadataService metadata,
        IFriendsService friends,
        IAvatarCache avatarCache,
        IStatusService statusService,
        IAccountSwitcherService accountSwitcherService,
        IMultiplayerService multiplayerService,
        Action<string, string>? showToast,
        Action<string, bool>? changeQuery,
        string actionKeyword,
        Func<ulong?> getActiveSteamId,
        Action invalidateUserCaches,
        string? localPersonaName,
        string defaultIconPath,
        Action<string, string, Exception> logException,
        Func<bool>? isNetworkAvailable = null,
        Func<bool>? isBigPictureRunning = null)
    {
        _localLibrary = localLibrary;
        _fuzzyMatcher = fuzzyMatcher;
        _storeSearch = storeSearch;
        _ownedGames = ownedGames;
        _userProfile = userProfile;
        _apiKeyStore = apiKeyStore;
        _settings = settings;
        _iconResolver = iconResolver;
        _metadata = metadata;
        _friends = friends;
        _avatarCache = avatarCache;
        _statusService = statusService;
        _accountSwitcherService = accountSwitcherService;
        _multiplayerService = multiplayerService;
        _showToast = showToast;
        _changeQuery = changeQuery;
        _actionKeyword = actionKeyword;
        _localPersonaName = localPersonaName;
        _defaultIconPath = defaultIconPath;
        _logException = logException;
        _isNetworkAvailable = isNetworkAvailable ?? NetworkInterface.GetIsNetworkAvailable;
        _isBigPictureRunning = isBigPictureRunning;
        _apiConfigRows = new ApiConfigRowBuilder(
            apiKeyStore, settings, saveSettings, invalidateUserCaches, getActiveSteamId, changeQuery, showToast, actionKeyword, defaultIconPath, logException);
    }

    public async Task<List<Result>> DispatchAsync(ParsedQuery query, CancellationToken ct) => query switch
    {
        ParsedQuery.Empty                    => await BuildEmptyResultsAsync(ct).ConfigureAwait(false),
        ParsedQuery.LibraryFilter f          => await BuildFilteredResultsAsync(f.Term, ct).ConfigureAwait(false),
        ParsedQuery.Me                       => await BuildMeResultsAsync(ct).ConfigureAwait(false),
        ParsedQuery.RecentlyAddedNeverPlayed => await BuildNewResultsAsync(ct).ConfigureAwait(false),
        ParsedQuery.ApiConfig api            => _apiConfigRows.Build(api),
        ParsedQuery.FriendsList fl           => await BuildFriendsListResultsAsync(fl.Filter, ct).ConfigureAwait(false),
        ParsedQuery.StatusSwitcher           => BuildStatusResults(),
        ParsedQuery.AccountSwitcher acs      => BuildAccountSwitcherResults(acs.ConfirmAccountName),
        ParsedQuery.MultiplayerWith mw       => await BuildMultiplayerResultsAsync(mw.FriendName, ct).ConfigureAwait(false),
        ParsedQuery.VerifyGame v             => await BuildVerifyResultsAsync(v.Filter, ct).ConfigureAwait(false),
        ParsedQuery.UninstallGame u          => await BuildUninstallResultsAsync(u.Filter, ct).ConfigureAwait(false),
        ParsedQuery.OpenSteamWindow w        => BuildOpenSteamWindowResults(w.Window),
        _ => []
    };

    private async Task<List<Result>> BuildEmptyResultsAsync(CancellationToken ct)
    {
        var games = await _localLibrary.GetInstalledGamesAsync(ct).ConfigureAwait(false);
        var friendsPlaying = await FetchFriendsPlayingMapAsync(ct).ConfigureAwait(false);
        var libraryRows = await BuildLibraryRowsAsync(
            games.Select(g => (g, (MatchResult?)null)).ToList(), friendsPlaying, ct).ConfigureAwait(false);

        return BuildEmptyResults(libraryRows);
    }

    public async Task<List<Result>> BuildFastEmptyResultsAsync(CancellationToken ct)
    {
        var games = await _localLibrary.GetInstalledGamesAsync(ct).ConfigureAwait(false);
        var libraryRows = games
            .Select(g => BuildLibraryResult(g, match: null, GameMetadata.Empty, friendsPlaying: 0))
            .ToList();

        return BuildEmptyResults(libraryRows);
    }

    private List<Result> BuildEmptyResults(List<Result> libraryRows)
    {
        var subtitle = string.IsNullOrEmpty(_localPersonaName)
            ? "Open the Steam client window"
            : $"Open the Steam client window · Signed in as {_localPersonaName}";

        var rows = new List<Result>(libraryRows.Count + 2)
        {
            new()
            {
                Title = "Launch Steam",
                SubTitle = subtitle,
                IcoPath = _defaultIconPath,
                Score = int.MaxValue,
                Action = _ => LaunchInBackground(SteamUriBuilder.MainWindow(), "Launch Steam failed")
            }
        };
        if (IsOffline()) rows.Add(OfflineRow());
        rows.AddRange(libraryRows);
        return rows;
    }

    private async Task<List<Result>> BuildFilteredResultsAsync(string filter, CancellationToken ct)
    {
        var libraryGames = await ResolveLibraryGamesAsync(filter, ct).ConfigureAwait(false);
        var friendsPlaying = await FetchFriendsPlayingMapAsync(ct).ConfigureAwait(false);

        // Same cap the fast pass applies, so the enriched results replace those rows
        // instead of expanding a 10-row list into every fuzzy match.
        if (libraryGames.Count >= CombinedResultCap)
        {
            return await BuildLibraryRowsAsync(
                libraryGames.Take(CombinedResultCap).ToList(), friendsPlaying, ct).ConfigureAwait(false);
        }

        var installedAppIds = (await _localLibrary.GetInstalledGamesAsync(ct).ConfigureAwait(false))
            .Select(g => g.AppId).ToHashSet();
        var storeGames = (await _storeSearch.SearchAsync(filter, ct).ConfigureAwait(false))
            .Where(g => !installedAppIds.Contains(g.AppId))
            .Take(CombinedResultCap - libraryGames.Count)
            .ToList();

        var rows = await BuildLibraryRowsAsync(libraryGames, friendsPlaying, ct).ConfigureAwait(false);
        rows.AddRange(await BuildStoreRowsAsync(storeGames, friendsPlaying, ct).ConfigureAwait(false));

        // Offline the store leg returns nothing, so an unmatched filter would otherwise
        // render as silence with no reason given.
        if (rows.Count == 0 && IsOffline()) return [OfflineRow()];
        return rows;
    }

    public async Task<List<Result>> BuildFastFilteredResultsAsync(string filter, CancellationToken ct)
    {
        var libraryGames = await ResolveLibraryGamesAsync(filter, ct).ConfigureAwait(false);
        return libraryGames
            .Take(CombinedResultCap)
            .Select(g => BuildLibraryResult(g.Game, g.Match, GameMetadata.Empty, friendsPlaying: 0))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<uint, int>> FetchFriendsPlayingMapAsync(CancellationToken ct)
    {
        try
        {
            var friends = await _friends.GetFriendsAsync(ct).ConfigureAwait(false);
            return friends
                .Where(f => f.CurrentGameAppId.HasValue)
                .GroupBy(f => f.CurrentGameAppId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logException(nameof(QueryDispatcher), "Failed to fetch friends-playing map; rendering games without friends-playing suffix", ex);
            return new Dictionary<uint, int>();
        }
    }

    private async Task<List<(InstalledGame Game, MatchResult? Match)>> ResolveLibraryGamesAsync(
        string? filter, CancellationToken ct)
    {
        var games = await _localLibrary.GetInstalledGamesAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(filter))
            return games.Select(g => (g, (MatchResult?)null)).ToList();

        var matched = new List<(InstalledGame Game, MatchResult? Match)>();
        foreach (var game in games)
        {
            var match = _fuzzyMatcher.Match(filter, game.Name);
            if (match.Score > 0) matched.Add((game, match));
        }
        return matched.OrderByDescending(m => m.Match!.Score).ToList();
    }

    private async Task<List<Result>> BuildLibraryRowsAsync(
        IReadOnlyList<(InstalledGame Game, MatchResult? Match)> games,
        IReadOnlyDictionary<uint, int> friendsPlaying,
        CancellationToken ct)
    {
        var meta = await FetchMetadataAsync(games.Select(g => g.Game.AppId), ct).ConfigureAwait(false);
        return games.Select(g => BuildLibraryResult(
            g.Game, g.Match, MetadataFor(meta, g.Game.AppId),
            friendsPlaying.TryGetValue(g.Game.AppId, out var n) ? n : 0)).ToList();
    }

    private async Task<List<Result>> BuildVerifyResultsAsync(string? filter, CancellationToken ct)
    {
        var libraryGames = await ResolveLibraryGamesAsync(filter, ct).ConfigureAwait(false);
        var friendsPlaying = await FetchFriendsPlayingMapAsync(ct).ConfigureAwait(false);
        var meta = await FetchMetadataAsync(libraryGames.Select(g => g.Game.AppId), ct).ConfigureAwait(false);
        return libraryGames.Select(g => BuildLibraryResult(
            g.Game, g.Match, MetadataFor(meta, g.Game.AppId),
            friendsPlaying.TryGetValue(g.Game.AppId, out var n) ? n : 0,
            subtitlePrefix: "Verify integrity · ",
            actionOverride: () => LaunchInBackground(SteamUriBuilder.Validate(g.Game.AppId), $"Verify failed: {g.Game.Name}"))).ToList();
    }

    public async Task<List<Result>> BuildFastVerifyResultsAsync(string? filter, CancellationToken ct)
    {
        var libraryGames = await ResolveLibraryGamesAsync(filter, ct).ConfigureAwait(false);
        return libraryGames.Select(g => BuildLibraryResult(
            g.Game, g.Match, GameMetadata.Empty, friendsPlaying: 0,
            subtitlePrefix: "Verify integrity · ",
            actionOverride: () => LaunchInBackground(SteamUriBuilder.Validate(g.Game.AppId), $"Verify failed: {g.Game.Name}"))).ToList();
    }

    private async Task<List<Result>> BuildUninstallResultsAsync(string? filter, CancellationToken ct)
    {
        var libraryGames = await ResolveLibraryGamesAsync(filter, ct).ConfigureAwait(false);
        if (libraryGames.Count == 0)
            return [SingleRow("No installed game matches", "Try a shorter title, or use `st` to browse installed games")];

        return libraryGames.Select(g => BuildUninstallCandidateRow(g.Game, g.Match)).ToList();
    }

    private Result BuildUninstallCandidateRow(InstalledGame game, MatchResult? match) => new()
    {
        Title = game.Name,
        SubTitle = $"Uninstall · {SubtitleFormatters.Bytes(game.SizeOnDiskBytes)} · Steam will ask for confirmation",
        IcoPath = game.IconPath ?? _defaultIconPath,
        TitleHighlightData = match?.MatchData,
        Score = match?.Score ?? 0,
        ContextData = new ContextData.Game(game.AppId, game.Name, game.FullInstallPath),
        Preview = PreviewBuilders.Game(game, GameMetadata.Empty, friendsPlaying: 0, game.IconPath ?? _defaultIconPath),
        Action = _ => LaunchInBackground(SteamUriBuilder.Uninstall(game.AppId), $"Uninstall failed: {game.Name}")
    };

    private sealed record SteamWindowRow(string Title, string SubTitle, string Glyph, string Uri);

    private static readonly Dictionary<SteamWindow, SteamWindowRow> SteamWindowRows = new()
    {
        [SteamWindow.Settings]    = new("Steam Settings",    "Open the Steam settings window",          "", SteamUriBuilder.OpenSettings()),
        [SteamWindow.Downloads]   = new("Steam Downloads",   "Open the Steam download manager",         "", SteamUriBuilder.OpenDownloads()),
        [SteamWindow.BigPicture]  = new("Big Picture Mode",  "Open Steam's TV and controller UI",       "", SteamUriBuilder.OpenBigPicture()),
        [SteamWindow.Screenshots] = new("Steam Screenshots", "Open the screenshot manager",             "", SteamUriBuilder.OpenScreenshots()),
        [SteamWindow.Redeem]      = new("Redeem Steam Key",  "Activate a product code on your account", "", SteamUriBuilder.OpenRedeem())
    };

    private List<Result> BuildOpenSteamWindowResults(SteamWindow window)
    {
        // Big Picture takes over the whole screen, so the useful action while it is open is
        // to leave it. Detection failing just falls through to the open row.
        if (window == SteamWindow.BigPicture && IsBigPictureRunning())
        {
            return
            [
                new()
                {
                    Title = "Exit Big Picture Mode",
                    SubTitle = "Big Picture is open - return to the Steam desktop UI",
                    IcoPath = _defaultIconPath,
                    Glyph = new GlyphInfo("Segoe Fluent Icons", SteamWindowRows[SteamWindow.BigPicture].Glyph),
                    Action = _ => LaunchInBackground(SteamUriBuilder.CloseBigPicture(), "Exit Big Picture Mode failed")
                }
            ];
        }

        if (!SteamWindowRows.TryGetValue(window, out var row)) return [];
        return
        [
            new()
            {
                Title = row.Title,
                SubTitle = row.SubTitle,
                IcoPath = _defaultIconPath,
                Glyph = new GlyphInfo("Segoe Fluent Icons", row.Glyph),
                Action = _ => LaunchInBackground(row.Uri, $"Open {row.Title} failed")
            }
        ];
    }

    private async Task<List<Result>> BuildStoreRowsAsync(
        IReadOnlyList<StoreGame> games,
        IReadOnlyDictionary<uint, int> friendsPlaying,
        CancellationToken ct)
    {
        var meta = await FetchMetadataAsync(games.Select(g => g.AppId), ct).ConfigureAwait(false);
        return games.Select(g => BuildStoreResult(
            g, MetadataFor(meta, g.AppId),
            friendsPlaying.TryGetValue(g.AppId, out var n) ? n : 0)).ToList();
    }

    /// <summary>
    /// Fetches metadata for the leading <see cref="MetadataEnrichmentCap"/> apps. Every app
    /// costs two Steam storefront requests, and callers pass whole libraries, so enrichment
    /// stops at the rows a user realistically scrolls to. Ids beyond the cap are absent from
    /// the result and their rows fall back to <see cref="GameMetadata.Empty"/> — playtime,
    /// last-played, and size come from the local manifest, so only the review summary and
    /// release details are missing.
    /// </summary>
    private async Task<Dictionary<uint, GameMetadata>> FetchMetadataAsync(
        IEnumerable<uint> appIds, CancellationToken ct)
    {
        var ids = appIds.Distinct().Take(MetadataEnrichmentCap).ToList();
        var metas = await Task.WhenAll(ids.Select(id => _metadata.GetAsync(id, ct))).ConfigureAwait(false);
        return ids.Zip(metas).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    private static GameMetadata MetadataFor(IReadOnlyDictionary<uint, GameMetadata> meta, uint appId) =>
        meta.TryGetValue(appId, out var m) ? m : GameMetadata.Empty;

    private async Task<List<Result>> BuildMeResultsAsync(CancellationToken ct)
    {
        if (!IsApiConfigured())
            return [ConfigureFirstHint()];

        var profile = await _userProfile.GetMyProfileAsync(ct).ConfigureAwait(false);
        if (profile is null)
            return [IsOffline() ? OfflineRow() : SingleRow("Could not load Steam profile", "Check your API key/Steam ID with `st api`, or try again later")];

        ulong.TryParse(_settings.SteamId64, out var steamId);
        var profileUri = SteamUriBuilder.Profile(steamId);
        var libraryUri = SteamUriBuilder.LibraryHome();

        return
        [
            new()
            {
                Title = profile.PersonaName,
                SubTitle = $"Steam Level {profile.SteamLevel?.ToString() ?? "?"} · {profile.OwnedGameCount} games · {profile.TotalPlaytimeMinutes / 60} hrs total",
                IcoPath = profile.AvatarUrl ?? _defaultIconPath,
                Preview = PreviewBuilders.UserProfile(profile, profile.AvatarUrl),
                Action = _ => LaunchInBackground(profileUri, "Open profile failed")
            },
            new()
            {
                Title = "Recent activity",
                SubTitle = $"{profile.RecentlyPlayedCount} games last 2 weeks · {profile.RecentPlaytimeMinutes / 60} hrs",
                IcoPath = _defaultIconPath,
                Preview = new Result.PreviewInfo
                {
                    Description = $"{profile.RecentlyPlayedCount} games played in the last 2 weeks{Environment.NewLine}{profile.RecentPlaytimeMinutes / 60} hrs recent playtime"
                },
                Action = _ => LaunchInBackground(libraryUri, "Open library failed")
            }
        ];
    }

    private async Task<List<Result>> BuildNewResultsAsync(CancellationToken ct)
    {
        if (!IsApiConfigured())
            return [ConfigureFirstHint()];

        var games = await _ownedGames.GetOwnedGamesAsync(ct).ConfigureAwait(false);
        if (games.Count == 0 && IsOffline())
            return [OfflineRow()];

        var unplayed = games.Where(g => g.PlaytimeMinutes == 0).ToList();
        if (unplayed.Count == 0)
            return [SingleRow("No never-played games found", "Steam reports playtime for every owned game returned by the API")];

        return unplayed
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Take(NewResultCap)
            .Select(g => new Result
            {
                Title = g.Name,
                SubTitle = "Never played",
                IcoPath = _iconResolver.Resolve(g.AppId),
                ContextData = new ContextData.Game(g.AppId, g.Name, InstallPath: null),
                Preview = new Result.PreviewInfo
                {
                    PreviewImagePath = _iconResolver.Resolve(g.AppId),
                    Description = $"{g.Name} ({g.AppId}){Environment.NewLine}Never played{Environment.NewLine}Owned game"
                },
                Action = _ => LaunchInBackground(SteamUriBuilder.RunGame(g.AppId), $"Launch failed: {g.Name}")
            })
            .ToList();
    }

    private async Task<List<Result>> BuildFriendsListResultsAsync(string? filter, CancellationToken ct)
    {
        if (!IsApiConfigured())
            return [ConfigureFirstHint()];

        var friends = await _friends.GetFriendsAsync(ct).ConfigureAwait(false);
        if (friends.Count == 0)
            return [IsOffline() ? OfflineRow() : SingleRow("No Steam friends loaded", "Steam returned no friends, your profile may be private, or the API call failed")];

        var favorites = _settings.FavoriteFriendIds.ToHashSet();

        if (string.IsNullOrWhiteSpace(filter))
        {
            var ordered = friends
                .OrderByDescending(f => favorites.Contains(f.SteamId64))
                .ThenBy(f => f.IsInGame ? 0 : f.PersonaState == PersonaState.Offline ? 2 : 1)
                .ThenBy(f => f.PersonaName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rows = await Task.WhenAll(ordered.Select(f =>
                BuildFriendResultAsync(f, favorites.Contains(f.SteamId64), match: null, ct))).ConfigureAwait(false);

            // Only on the unfiltered list: when a filter is present the user is after a
            // specific person, not the window.
            return [BuildOpenFriendsRow(friends), .. rows];
        }

        var matched = new List<(Friend Friend, MatchResult Match)>();
        foreach (var friend in friends)
        {
            var match = _fuzzyMatcher.Match(filter, friend.PersonaName);
            if (match.Score > 0) matched.Add((friend, match));
        }

        if (matched.Count == 0)
            return [SingleRow($"No friend matches \"{filter}\"", "Try a shorter name, or run `st friends` to browse the loaded list")];

        var top = matched
            .OrderByDescending(m => favorites.Contains(m.Friend.SteamId64))
            .ThenByDescending(m => m.Match.Score)
            .Take(CombinedResultCap)
            .ToList();

        var matchRows = await Task.WhenAll(top.Select(m =>
            BuildFriendResultAsync(m.Friend, favorites.Contains(m.Friend.SteamId64), m.Match, ct))).ConfigureAwait(false);
        return matchRows.ToList();
    }

    private Result BuildOpenFriendsRow(IReadOnlyList<Friend> friends)
    {
        var inGame = friends.Count(f => f.IsInGame);
        var online = friends.Count(f => !f.IsInGame && f.PersonaState != PersonaState.Offline);

        var parts = new List<string>(3) { "Open the Steam friends list" };
        if (online > 0) parts.Add($"{online} online");
        if (inGame > 0) parts.Add($"{inGame} in game");

        return new Result
        {
            Title = "Steam Friends",
            SubTitle = string.Join(" · ", parts),
            IcoPath = _defaultIconPath,
            Score = int.MaxValue,
            Action = _ => LaunchInBackground(SteamUriBuilder.OpenFriends(), "Open Steam friends list failed")
        };
    }

    private async Task<Result> BuildFriendResultAsync(
        Friend friend,
        bool isFavorite,
        MatchResult? match,
        CancellationToken ct)
    {
        var iconPath = await _avatarCache.GetLocalPathAsync(friend.AvatarUrl, ct).ConfigureAwait(false)
                     ?? _defaultIconPath;
        var dmUri = SteamUriBuilder.FriendsMessage(friend.SteamId64);

        return new Result
        {
            Title = friend.PersonaName,
            SubTitle = FriendRowFormatter.BuildSubtitle(friend, isFavorite),
            IcoPath = iconPath,
            TitleHighlightData = match?.MatchData,
            Score = match?.Score ?? 0,
            ContextData = new ContextData.Friend(friend.SteamId64, friend.PersonaName, isFavorite, friend.IsInGame, friend.CurrentGameAppId),
            Preview = PreviewBuilders.Friend(friend, isFavorite, iconPath),
            Action = _ => LaunchInBackground(dmUri, $"Open chat failed: {friend.PersonaName}")
        };
    }

    private List<Result> BuildStatusResults() =>
    [
        BuildStatusRow("Online",    "Show as online to friends",       PersonaStateDesired.Online),
        BuildStatusRow("Invisible", "Appear offline; can still chat",  PersonaStateDesired.Invisible),
        BuildStatusRow("Offline",   "Hide and disconnect from friends", PersonaStateDesired.Offline)
    ];

    private Result BuildStatusRow(string label, string subtitle, PersonaStateDesired desired) => new()
    {
        Title = label,
        SubTitle = subtitle,
        IcoPath = _defaultIconPath,
        Action = ctx =>
        {
            _ = Task.Run(() =>
            {
                try
                {
                    _statusService.Set(desired);
                    _showToast?.Invoke($"Status set to {label}", "Steam handled it instantly");
                }
                catch (Exception ex)
                {
                    _logException(nameof(QueryDispatcher), $"Set status to {label} failed", ex);
                    _showToast?.Invoke("Set status failed", "Could not launch the steam:// URI — see Flow's log");
                }
            });
            return true;
        }
    };

    private List<Result> BuildAccountSwitcherResults(string? confirmAccountName)
    {
        var accounts = _accountSwitcherService.GetKnownAccounts();
        if (accounts.Count == 0)
            return [SingleRow("No saved Steam accounts found", "Steam is not installed, or this PC has no remembered Steam logins")];

        var current = accounts.FirstOrDefault(a => a.IsCurrent);
        var switchable = accounts
            .Where(a => !a.IsCurrent)
            .OrderByDescending(a => a.LastLoginUnix ?? 0L)
            .ToList();

        if (confirmAccountName is not null)
        {
            var target = switchable.FirstOrDefault(a =>
                string.Equals(a.AccountName, confirmAccountName, StringComparison.OrdinalIgnoreCase));
            if (target is null)
                return [SingleRow("Account not found", $"No saved account matches \"{confirmAccountName}\"")];
            return [BuildAccountConfirmRow(target)];
        }

        var rows = new List<Result>(switchable.Count + 2);
        if (current is not null)
            rows.Add(BuildCurrentAccountRow(current));

        if (switchable.Count == 0)
        {
            rows.Add(SingleRow(
                "No other accounts saved on this machine",
                "Sign into another Steam account with Remember me enabled, then try again"));
            return rows;
        }

        rows.AddRange(switchable.Select(BuildAccountRow));
        return rows;
    }

    private Result BuildCurrentAccountRow(KnownAccount account) => new()
    {
        Title = $"Current: {account.PersonaName}",
        SubTitle = BuildAccountSubtitle(account, "Already active"),
        IcoPath = account.AvatarPath ?? _defaultIconPath,
        Score = int.MaxValue,
        Preview = PreviewBuilders.Account(account, "Already active", account.AvatarPath),
        Action = _ =>
        {
            _showToast?.Invoke("Already using this account", account.AccountName);
            return false;
        }
    };

    private Result BuildAccountRow(KnownAccount account) => new()
    {
        Title = account.PersonaName,
        SubTitle = BuildAccountSubtitle(account, "Press Enter to confirm switch"),
        IcoPath = account.AvatarPath ?? _defaultIconPath,
        Preview = PreviewBuilders.Account(account, "Available to switch", account.AvatarPath),
        Action = ctx =>
        {
            _changeQuery?.Invoke($"{_actionKeyword} switch confirm {account.AccountName}", true);
            return false;
        }
    };

    private Result BuildAccountConfirmRow(KnownAccount account) => new()
    {
        Title = $"Confirm: switch to {account.PersonaName}",
        SubTitle = BuildConfirmAccountSubtitle(account),
        IcoPath = account.AvatarPath ?? _defaultIconPath,
        Preview = PreviewBuilders.Account(account, "Confirm switch target", account.AvatarPath),
        Action = ctx =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _accountSwitcherService.SwitchToAsync(account.SteamId64, CancellationToken.None).ConfigureAwait(false);
                    var (toastTitle, toastBody) = result.Status switch
                    {
                        SwitchOutcome.Ok                      => ($"Switching to {account.PersonaName}", $"Steam restarting as {account.AccountName}. If a sign-in screen appears, the saved session expired — sign in once with Remember me."),
                        SwitchOutcome.SteamNotInstalled       => ("Switch failed", "Steam isn't installed"),
                        SwitchOutcome.AccountNotFound         => ("Switch failed", $"Account \"{account.AccountName}\" not found in loginusers.vdf"),
                        SwitchOutcome.KillFailed              => ("Switch failed", "Could not stop the running Steam process"),
                        SwitchOutcome.LoginUsersWriteFailed   => ("Switch failed", "Could not update loginusers.vdf"),
                        SwitchOutcome.RegistryWriteFailed     => ("Switch failed", "Could not write to HKCU\\Software\\Valve\\Steam"),
                        SwitchOutcome.GameRunning             => ("Quit your game/download first", "Refused to kill Steam mid-session"),
                        _                                     => ("Switch failed", "Unknown error — see Flow's log")
                    };
                    _showToast?.Invoke(toastTitle, toastBody);
                }
                catch (Exception ex)
                {
                    _logException(nameof(QueryDispatcher), $"Switch to {account.AccountName} failed", ex);
                    _showToast?.Invoke("Switch failed", "Unexpected error — see Flow's log");
                }
            });
            return true;
        }
    };

    private static string BuildAccountSubtitle(KnownAccount account, string status)
    {
        var parts = new List<string> { status, account.AccountName };
        if (!account.RememberPassword)
            parts.Add("password not remembered");
        if (account.LastLoginUnix is { } unix && unix > 0)
            parts.Add($"last login {SubtitleFormatters.LastPlayedRelative(DateTimeOffset.FromUnixTimeSeconds(unix))}");
        return string.Join(" · ", parts);
    }

    private static string BuildConfirmAccountSubtitle(KnownAccount account)
    {
        var parts = new List<string>();
        if (!account.RememberPassword)
            parts.Add("Steam may ask for this account's password");
        parts.Add($"Target account: {account.AccountName}");
        parts.Add("Steam will close and reopen");
        parts.Add("quit running games first");
        return string.Join(" · ", parts);
    }

    private async Task<List<Result>> BuildMultiplayerResultsAsync(string friendName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(friendName))
            return [SingleRow("Usage: st multi <friend>", "Type part of a friend's Steam persona name")];

        var result = await _multiplayerService.FindSharedAsync(friendName, ct).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case MultiplayerOutcome.NoFriendMatch:
                return [SingleRow($"No matching friend for \"{friendName}\"", "Try a shorter name, or run `st friends` to browse the loaded list")];
            case MultiplayerOutcome.PrivateOrEmpty:
                return [SingleRow($"{result.PersonaName} — game list private or empty",
                    "Steam did not return their owned games; their profile or game details may be private")];
            case MultiplayerOutcome.Match when result.SharedGames.Count == 0:
                return [SingleRow($"No multiplayer games in common with {result.PersonaName}",
                    "Shared games were found, but none had Steam multiplayer/co-op category tags")];
            case MultiplayerOutcome.Match:
            {
                var rows = new List<Result>(result.SharedGames.Count + 1);
                if (result.Friend is { } friend)
                    rows.Add(await BuildMultiplayerFriendOverviewRowAsync(friend, ct).ConfigureAwait(false));
                rows.AddRange(result.SharedGames.Select(g => BuildMultiplayerGameRow(g, result.PersonaName!)));
                return rows;
            }
            default:
                return [];
        }
    }

    private async Task<Result> BuildMultiplayerFriendOverviewRowAsync(Friend friend, CancellationToken ct)
    {
        var iconPath = await _avatarCache.GetLocalPathAsync(friend.AvatarUrl, ct).ConfigureAwait(false)
                     ?? _defaultIconPath;
        var isFavorite = _settings.FavoriteFriendIds.Contains(friend.SteamId64);
        var dmUri = SteamUriBuilder.FriendsMessage(friend.SteamId64);
        return new Result
        {
            Title = friend.PersonaName,
            SubTitle = FriendRowFormatter.BuildSubtitle(friend, isFavorite),
            IcoPath = iconPath,
            ContextData = new ContextData.Friend(friend.SteamId64, friend.PersonaName, isFavorite, friend.IsInGame, friend.CurrentGameAppId),
            Preview = PreviewBuilders.Friend(friend, isFavorite, iconPath),
            Action = _ => LaunchInBackground(dmUri, $"Open chat failed: {friend.PersonaName}")
        };
    }

    private Result BuildMultiplayerGameRow(SharedMultiplayerGame game, string friendPersonaName) => new()
    {
        Title = game.Name,
        SubTitle = MultiplayerRowFormatter.BuildSubtitle(friendPersonaName, game),
        IcoPath = _iconResolver.Resolve(game.AppId),
        ContextData = new ContextData.Game(game.AppId, game.Name, InstallPath: null),
        Preview = PreviewBuilders.MultiplayerGame(game, friendPersonaName, _iconResolver.Resolve(game.AppId)),
        Action = _ => LaunchInBackground(SteamUriBuilder.RunGame(game.AppId), $"Launch failed: {game.Name}")
    };

    private Result BuildLibraryResult(
        InstalledGame game,
        MatchResult? match,
        GameMetadata meta,
        int friendsPlaying,
        string? subtitlePrefix = null,
        Func<bool>? actionOverride = null) => new()
    {
        Title = game.Name,
        SubTitle = (subtitlePrefix ?? string.Empty) + LibraryRowFormatter.BuildSubtitle(game, meta, friendsPlaying),
        IcoPath = game.IconPath ?? _defaultIconPath,
        TitleHighlightData = match?.MatchData,
        Score = match?.Score ?? 0,
        ContextData = new ContextData.Game(game.AppId, game.Name, game.FullInstallPath),
        Preview = PreviewBuilders.Game(game, meta, friendsPlaying, game.IconPath ?? _defaultIconPath),
        Action = _ => actionOverride?.Invoke() ?? LaunchInBackground(SteamUriBuilder.RunGame(game.AppId), $"Launch failed: {game.Name}")
    };

    private Result BuildStoreResult(StoreGame game, GameMetadata meta, int friendsPlaying)
    {
        var ownedNotInstalled = game.IsOwned;
        var uri = ownedNotInstalled
            ? SteamUriBuilder.LibraryDetails(game.AppId)
            : SteamUriBuilder.Store(game.AppId);

        return new Result
        {
            Title = game.Name,
            SubTitle = StoreRowFormatter.BuildSubtitle(game, meta, friendsPlaying),
            IcoPath = game.IconUrl ?? _iconResolver.Resolve(game.AppId),
            ContextData = new ContextData.Game(game.AppId, game.Name, InstallPath: null),
            Preview = PreviewBuilders.StoreGame(game, meta, friendsPlaying, game.IconUrl ?? _iconResolver.Resolve(game.AppId)),
            Action = _ => LaunchInBackground(uri, $"Open failed: {game.Name}")
        };
    }

    private bool LaunchInBackground(string uri, string failureMessage)
    {
        _ = Task.Run(() =>
        {
            try { SteamProcessRunner.Launch(uri); }
            catch (Exception ex) { _logException(nameof(QueryDispatcher), failureMessage, ex); }
        });
        return true;
    }

    private bool IsApiConfigured() =>
        _apiKeyStore.IsConfigured && !string.IsNullOrEmpty(_settings.SteamId64);

    private Result ConfigureFirstHint() => SingleRow(
        "Configure your Steam Web API key + Steam ID first",
        "Run `st api <key>` and `st api id <steamid64>`");

    private bool IsOffline() => !_isNetworkAvailable();

    // Window enumeration can fail for reasons outside our control; treating that as
    // "not running" keeps the row at its old open-only behaviour rather than erroring.
    private bool IsBigPictureRunning()
    {
        if (_isBigPictureRunning is null) return false;
        try { return _isBigPictureRunning(); }
        catch (Exception ex)
        {
            _logException(nameof(QueryDispatcher), "Big Picture detection failed; assuming it is closed", ex);
            return false;
        }
    }

    private Result OfflineRow() => new()
    {
        Title = "You're offline",
        SubTitle = "Connect to the internet to load Steam friends, profile, and store data",
        IcoPath = _defaultIconPath,
        Score = int.MaxValue - 1
    };

    private Result SingleRow(string title, string subtitle) => new()
    {
        Title = title,
        SubTitle = subtitle,
        IcoPath = _defaultIconPath
    };
}
