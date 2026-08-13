using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.Satisfactory.Services;
using KroModIx.Plugin.Satisfactory.Services.Ficsit;
using KroModIx.Plugin.Satisfactory.Views;

namespace KroModIx.Plugin.Satisfactory;

public sealed class SatisfactoryPlugin : IGameModPlugin, IUpdateNotifier
{
    public PluginMetadata Metadata { get; } = new(
        Id: "kroste.satisfactory",
        DisplayName: "Satisfactory Mod-Manager",
        Version: "0.6.0",
        Author: "Kroste",
        Description: "Mod-Manager für Satisfactory (Coffee Stain). ficsit.app-Katalog " +
            "via GraphQL, .smod-Direct-Download, Install in FactoryGame/Mods. Kroste-" +
            "Card-Look, Auto-Refresh via FileSystemWatcher. v0.6.0: DE+EN-Uebersetzung " +
            "aller User-facing Strings. Ab v0.1.0 grüner ↑-Badge auf der Satisfactory-" +
            "Kachel bei neuen ficsit-Katalog-Einträgen.");

    public IReadOnlyList<GameTarget> Targets { get; } = new[]
    {
        new GameTarget("satisfactory", "Satisfactory",
            SteamAppId: 526870,
            AlternativeExecutableNames: new[] { "FactoryGame.exe", "FactoryGame" },
            Platforms: Platforms.Both),
    };

    private IHostServices? _host;
    private SatisfactoryPaths? _paths;
    private FicsitSettingsService? _ficsitSettings;
    private FicsitApiClient? _ficsitApi;
    private FicsitCatalogService? _ficsitCatalog;
    private FicsitUpdateTracker? _updateTracker;
    private InstalledUpdatesTracker? _installedUpdatesTracker;
    private SmodMetadataReader? _smodReader;
    private DownloadEventBus? _downloadBus;
    private IReadOnlyList<DetectedGame> _activatedGames = Array.Empty<DetectedGame>();
    private readonly Dictionary<string, SmodInstallService> _installers = new();
    private readonly Dictionary<string, InstalledUpdatesChecker> _updateCheckers = new();
    private readonly SatisfactoryPathResolver _pathResolver = new();

    public Task InitializeAsync(IHostServices host, IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        Strings.Init(host.Localization);
        _host = host;
        _paths = new SatisfactoryPaths(host);
        _ficsitSettings = new FicsitSettingsService(_paths);
        _ficsitApi = new FicsitApiClient(host.CreateHttpClient("ficsit"));
        _ficsitCatalog = new FicsitCatalogService(_ficsitApi, _ficsitSettings, _paths);
        _updateTracker = new FicsitUpdateTracker(_paths);
        _installedUpdatesTracker = new InstalledUpdatesTracker(_paths);
        _smodReader = new SmodMetadataReader();
        _downloadBus = new DownloadEventBus();
        _activatedGames = activatedGames;

        foreach (var game in activatedGames)
        {
            var modsDir = _pathResolver.GetModsDir(game);
            if (modsDir is null)
            {
                host.Logger.Warn("Satisfactory: konnte keinen Mods-Pfad ableiten für {Game}",
                    game.Target.DisplayName);
                continue;
            }
            var installer = new SmodInstallService(modsDir, _paths.DownloadsDir, _smodReader);
            _installers[game.Target.GameId] = installer;
            _updateCheckers[game.Target.GameId] = new InstalledUpdatesChecker(
                installer, _ficsitApi, _installedUpdatesTracker, host);
            host.Logger.Info("Satisfactory initialisiert: mods={Mods}, downloads={Downloads}",
                modsDir, _paths.DownloadsDir);
        }

        // Auto-Check der installierten Mod-Updates im Hintergrund — damit der
        // grüne ↑-Badge auf der Sidebar-Kachel sofort nach Plugin-Load sichtbar
        // ist ohne dass der User erst „Updates prüfen" im Installiert-Tab klickt.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15), ct);
            foreach (var checker in _updateCheckers.Values)
            {
                try { await checker.CheckAsync(ct: ct); }
                catch (Exception ex) { host.Logger.Debug(ex, "Auto-Update-Check fehlgeschlagen"); }
            }
        }, ct);

        // Skill Kernprinzip 6b: nach JEDEM Install/Update (Row-Install,
        // Bulk-Install, Update-Row) den Checker re-triggern — sonst bleibt der
        // Sidebar-Kachel-Badge auf altem Zählerstand hängen. DownloadEventBus.
        // ModInstalled ist das zentrale Signal für „was änderte sich im Mods-
        // Ordner" — hier abfangen erspart Duplicate-Wiring in jedem VM.
        _downloadBus.ModInstalled += (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                foreach (var checker in _updateCheckers.Values)
                {
                    try { await checker.CheckAsync(); }
                    catch (Exception ex) { host.Logger.Debug(ex, "Post-Install Update-Check fehlgeschlagen"); }
                }
            });
        };

        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (!_installers.TryGetValue(game.Target.GameId, out var installer) || _host is null
            || _paths is null || _downloadBus is null || _ficsitSettings is null
            || _ficsitApi is null || _ficsitCatalog is null || _smodReader is null
            || _updateTracker is null || _installedUpdatesTracker is null
            || !_updateCheckers.TryGetValue(game.Target.GameId, out var updatesChecker))
            yield break;

        yield return new InstalledTab(installer, _paths, _downloadBus, _host,
            _ficsitApi, _ficsitSettings, updatesChecker);
        yield return new CatalogTab(_ficsitCatalog, _ficsitSettings, _ficsitApi,
            installer, _downloadBus, _paths, _updateTracker, _host);
        yield return new DownloadsTab(installer, _downloadBus, _host,
            _ficsitApi, _ficsitSettings, _paths);
        yield return new SettingsTab(_ficsitSettings, _host);
    }

    public Task ShutdownAsync()
    {
        _ficsitApi?.Dispose();
        _host?.Logger.Info("Satisfactory shutdown");
        return Task.CompletedTask;
    }

    // ---- IUpdateNotifier ----

    /// <summary>Meldet ausstehende Mod-Updates fuer INSTALLIERTE Mods
    /// (<see cref="InstalledUpdatesTracker"/>). Neue Katalog-Eintraege
    /// zaehlen bewusst NICHT als Badge — der gruene ↑-Pfeil ist ein
    /// Actionable-Signal, kein Community-News-Signal.</summary>
    public Task<IReadOnlyList<GameUpdateInfo>> GetPendingUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_installedUpdatesTracker is null || _activatedGames.Count == 0)
            return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(Array.Empty<GameUpdateInfo>());

        // Nur echte Mod-Updates fuer INSTALLIERTE Mods zaehlen. Neue
        // ficsit-Katalog-Eintraege waren bis v0.5 auch im Badge — das ist
        // aber ein Community-News-Signal, nicht Actionable. Der gruene ↑-
        // Pfeil steht fuer „User sollte was updaten".
        var installedCount = _installedUpdatesTracker.PendingCount;
        if (installedCount <= 0)
            return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(Array.Empty<GameUpdateInfo>());

        var summary = _installedUpdatesTracker.Summary is { Length: > 0 } s
            ? s
            : string.Format(Strings.T("notify.updates_hint_summary_fallback"), installedCount);
        var result = _activatedGames
            .Where(g => g.Target.SteamAppId is int)
            .Select(g => new GameUpdateInfo(g.Target.SteamAppId!.Value, installedCount, summary))
            .ToList();
        return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(result);
    }

    // ---- Tab-Contributions ----

    private sealed class InstalledTab : IGameTabContribution
    {
        private readonly SmodInstallService _installer;
        private readonly SatisfactoryPaths _paths;
        private readonly DownloadEventBus _bus;
        private readonly IHostServices _host;
        private readonly FicsitApiClient _api;
        private readonly FicsitSettingsService _settings;
        private readonly InstalledUpdatesChecker _updatesChecker;
        public InstalledTab(SmodInstallService installer, SatisfactoryPaths paths,
            DownloadEventBus bus, IHostServices host,
            FicsitApiClient api, FicsitSettingsService settings,
            InstalledUpdatesChecker updatesChecker)
        {
            _installer = installer; _paths = paths; _bus = bus; _host = host;
            _api = api; _settings = settings; _updatesChecker = updatesChecker;
        }
        public string Id => "installed";
        public string Label => Strings.T("tab.installed");
        public string Icon => "\U0001F3ED"; // 🏭
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new InstalledSmodsView { DataContext = new InstalledSmodsViewModel(
                _installer, _paths, _bus, _host, _api, _settings, _updatesChecker) };
    }

    private sealed class CatalogTab : IGameTabContribution
    {
        private readonly FicsitCatalogService _catalog;
        private readonly FicsitSettingsService _settings;
        private readonly FicsitApiClient _api;
        private readonly SmodInstallService _installer;
        private readonly DownloadEventBus _downloadBus;
        private readonly SatisfactoryPaths _paths;
        private readonly FicsitUpdateTracker _updateTracker;
        private readonly IHostServices _host;
        public CatalogTab(FicsitCatalogService catalog, FicsitSettingsService settings,
            FicsitApiClient api, SmodInstallService installer, DownloadEventBus downloadBus,
            SatisfactoryPaths paths, FicsitUpdateTracker updateTracker, IHostServices host)
        {
            _catalog = catalog; _settings = settings; _api = api;
            _installer = installer; _downloadBus = downloadBus; _paths = paths;
            _updateTracker = updateTracker; _host = host;
        }
        public string Id => "catalog";
        public string Label => Strings.T("tab.catalog");
        public string Icon => "\U0001F30D"; // 🌍
        public int Order => 10;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new CatalogView { DataContext = new CatalogViewModel(_catalog, _settings, _api,
                _installer, _downloadBus, _paths, _updateTracker, _host) };
    }

    private sealed class DownloadsTab : IGameTabContribution
    {
        private readonly SmodInstallService _installer;
        private readonly DownloadEventBus _bus;
        private readonly IHostServices _host;
        private readonly FicsitApiClient _api;
        private readonly FicsitSettingsService _settings;
        private readonly SatisfactoryPaths _paths;
        public DownloadsTab(SmodInstallService installer, DownloadEventBus bus, IHostServices host,
            FicsitApiClient api, FicsitSettingsService settings, SatisfactoryPaths paths)
        {
            _installer = installer; _bus = bus; _host = host;
            _api = api; _settings = settings; _paths = paths;
        }
        public string Id => "downloads";
        public string Label => Strings.T("tab.downloads");
        public string Icon => "\U0001F4E5"; // 📥
        public int Order => 20;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new DownloadsView { DataContext = new DownloadsViewModel(_installer, _bus, _host,
                _api, _settings, _paths) };
    }

    private sealed class SettingsTab : IGameTabContribution
    {
        private readonly FicsitSettingsService _settings;
        private readonly IHostServices _host;
        public SettingsTab(FicsitSettingsService settings, IHostServices host)
        { _settings = settings; _host = host; }
        public string Id => "settings";
        public string Label => Strings.T("tab.settings");
        public string Icon => "⚙"; // ⚙
        public int Order => 30;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new SettingsView { DataContext = new SettingsViewModel(_settings, _host) };
    }
}
