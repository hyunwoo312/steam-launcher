using System.IO;
using System.Net.Http;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Flow.Launcher.Plugin.SteamLauncher.Security;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Settings;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;

namespace Flow.Launcher.Plugin.SteamLauncher;

public sealed class Main : IAsyncPlugin, IContextMenu, IDisposable
{
    private QueryDispatcher? _dispatcher;
    private ContextMenuBuilder? _contextMenuBuilder;
    private PluginInitContext? _context;
    private MemoryCacheStore? _cache;
    private HttpClient? _httpClient;
    private string _defaultIconPath = string.Empty;
    private Action<string, string, Exception> _logException = (_, _, _) => { };

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;

        _logException = (cn, msg, ex) => context.API.LogException(cn, msg, ex);

        var registry = new RegistryReader();
        var vdfParser = new VdfParser();
        var pathResolver = new SteamPathResolver(registry, vdfParser);
        var iconResolver = new GameIconResolver(pathResolver);
        var localLibrary = new LocalLibraryService(pathResolver, vdfParser, iconResolver, _logException);
        var fuzzyMatcher = new FlowFuzzyMatcher(context.API);

        var settings = context.API.LoadSettingJsonStorage<PluginSettings>();
        var pluginDir = context.CurrentPluginMetadata.PluginDirectory;
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FlowLauncher", "Settings", "Plugins", context.CurrentPluginMetadata.ID);
        Directory.CreateDirectory(settingsPath);

        if (string.IsNullOrEmpty(settings.SteamId64)
            && pathResolver.GetActiveSteamId64() is { } detectedId)
        {
            settings.SteamId64 = detectedId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.API.SaveSettingJsonStorage<PluginSettings>();
        }

        var apiKeyStore = new DpapiApiKeyStore(settingsPath);
        _cache = new MemoryCacheStore(
            CachePolicies.Default,
            persistenceDir: settings.Cache.PersistToDisk ? Path.Combine(settingsPath, "cache") : null);

        _httpClient = BuildHttpClient(context.CurrentPluginMetadata.Version);
        var webApiClient = new SteamWebApiClient(_httpClient, apiKeyStore);

        var ownedGames = new OwnedGamesService(webApiClient, _cache, settings);
        var userProfile = new UserProfileService(webApiClient, ownedGames, settings);
        var storeSearch = new StoreSearchService(webApiClient, ownedGames, _cache, settings);
        var gameMetadata = new GameMetadataService(webApiClient, _cache);

        var avatarsDir = Path.Combine(settingsPath, "avatars");
        var avatarCache = new AvatarCache(avatarsDir, _httpClient);
        var friends = new FriendsService(webApiClient, _cache, settings);

        var processController = new SteamProcessController(pathResolver);

        var statusService = new StatusService(SteamProcessRunner.Launch);

        var vdfWriter = new VdfWriter();
        var registryWriter = new RegistryWriter();
        var accountSwitcherService = new AccountSwitcherService(
            pathResolver, vdfParser, vdfWriter, registryWriter, registry, processController, _logException);

        var multiplayerService = new MultiplayerService(friends, ownedGames, webApiClient, gameMetadata, fuzzyMatcher);

        _defaultIconPath = Path.Combine(pluginDir, "Images", "steam.png");
        Action saveSettings = () => context.API.SaveSettingJsonStorage<PluginSettings>();
        var localPersonaName = pathResolver.GetActivePersonaName();
        Action<string, string> showToast = (title, body) => context.API.ShowMsg(title, body);
        Action<string, bool> changeQuery = (q, requery) => context.API.ChangeQuery(q, requery);
        var actionKeyword = context.CurrentPluginMetadata.ActionKeywords.FirstOrDefault() ?? "st";
        Action invalidateUserCaches = () =>
        {
            _cache.InvalidateDomain(CachePolicies.OwnedGames);
            _cache.InvalidateDomain(CachePolicies.FriendList);
            _cache.InvalidateDomain(CachePolicies.PlayerSummaries);
        };
        _dispatcher = new QueryDispatcher(
            localLibrary, fuzzyMatcher, storeSearch, ownedGames, userProfile,
            apiKeyStore, settings, saveSettings, iconResolver, gameMetadata,
            friends, avatarCache,
            statusService, accountSwitcherService, multiplayerService,
            showToast, changeQuery, actionKeyword,
            pathResolver.GetActiveSteamId64,
            invalidateUserCaches,
            localPersonaName, _defaultIconPath, _logException);
        _contextMenuBuilder = new ContextMenuBuilder(context.API, settings, saveSettings, _defaultIconPath, _logException);

        _ = Task.Run(async () =>
        {
            try
            {
                await localLibrary.GetInstalledGamesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logException(nameof(Main), "Pre-warming library cache failed", ex);
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await friends.WarmUpAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logException(nameof(Main), "Pre-warming friends cache failed", ex);
            }
        });

        return Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(global::Flow.Launcher.Plugin.Query query, CancellationToken token)
    {
        if (_dispatcher is null) return [];

        try
        {
            var parsed = QueryParser.Parse(query.Search);
            return await _dispatcher.DispatchAsync(parsed, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (SteamPathNotFoundException ex) { return SingleResult("Steam not found", ex.Message); }
        catch (Exception ex)
        {
            _logException(nameof(Main), $"Query '{query.Search}' failed", ex);
            return SingleResult("Plugin error", "Check Flow's log for details");
        }
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (_contextMenuBuilder is not null && selectedResult.ContextData is ContextData data)
            return _contextMenuBuilder.Build(data);
        return [];
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _cache?.Dispose();
    }

    private static HttpClient BuildHttpClient(string pluginVersion)
    {
        var retryHandler = new SteamHttpRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(250))
        {
            InnerHandler = new HttpClientHandler()
        };
        var http = new HttpClient(retryHandler)
        {
            BaseAddress = new Uri("https://api.steampowered.com/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Flow.Launcher.Plugin.SteamLauncher/{pluginVersion}");
        return http;
    }

    private List<Result> SingleResult(string title, string subtitle) =>
    [
        new()
        {
            Title = title,
            SubTitle = subtitle,
            IcoPath = _defaultIconPath
        }
    ];
}
