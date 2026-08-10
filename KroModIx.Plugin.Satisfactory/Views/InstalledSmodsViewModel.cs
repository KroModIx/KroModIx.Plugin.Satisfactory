using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.Satisfactory.Services;
using KroModIx.Plugin.Satisfactory.Services.Ficsit;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>VM für den „Installiert"-Tab. Zeigt Mods aus
/// <c>FactoryGame/Mods/</c>. Refresh off-thread (perf.md Regel 0).
/// Kein Enable/Disable-Toggle in v0.1.0 (das läuft in SMM über ein separates
/// profiles.json — kommt in v0.2). Nexus-Enrichment via ficsit-API pro Row
/// throttled 250 ms.</summary>
public sealed partial class InstalledSmodsViewModel : ObservableObject, IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SmodInstallService _installer;
    private readonly SatisfactoryPaths _paths;
    private readonly DownloadEventBus _downloadBus;
    private readonly IHostServices _host;
    private readonly FicsitApiClient _api;
    private readonly FicsitSettingsService _ficsitSettings;

    private FileSystemWatcher? _watcher;

    public InstalledSmodsViewModel(SmodInstallService installer, SatisfactoryPaths paths,
        DownloadEventBus downloadBus, IHostServices host,
        FicsitApiClient api, FicsitSettingsService ficsitSettings)
    {
        _installer = installer;
        _paths = paths;
        _downloadBus = downloadBus;
        _host = host;
        _api = api;
        _ficsitSettings = ficsitSettings;
        ModsDir = installer.ModsDir;
        SetupWatcher();
        RefreshCommand.Execute(null);

        _downloadBus.ModInstalled += (_, _) =>
            Dispatcher.UIThread.Post(() => Refresh());
    }

    public string ModsDir { get; }

    public ObservableCollection<SmodInstalledRow> Mods { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private SmodInstalledRow? _selected;
    public bool HasSelection => Selected is not null;

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _searchText = "";

    private List<SmodInstalledRow> _all = new();

    partial void OnSelectedChanged(SmodInstalledRow? value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void SetupWatcher()
    {
        try
        {
            if (Directory.Exists(ModsDir))
            {
                _watcher = new FileSystemWatcher(ModsDir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                };
                _watcher.Created += (_, _) => Dispatcher.UIThread.Post(() => Refresh());
                _watcher.Deleted += (_, _) => Dispatcher.UIThread.Post(() => Refresh());
                _host.Logger.Info("Satisfactory installed watcher aktiv: {Dir}", ModsDir);
            }
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Satisfactory installed watcher fehlgeschlagen");
        }
    }

    /// <summary>Refresh off-thread (perf.md Regel 0). ListInstalled enumeriert
    /// Ordner + liest data.json pro Mod. Bei 30+ Mods sonst UI-Blocker.</summary>
    [RelayCommand]
    private void Refresh()
    {
        Summary = "Installierte Mods werden gelesen …";
        _ = Task.Run(async () =>
        {
            List<InstalledSmodMod>? mods = null;
            string? error = null;
            try { mods = _installer.ListInstalled().ToList(); }
            catch (Exception ex) { error = ex.Message; Log.Warn(ex, "Satisfactory: Mod-Liste-Load fehlgeschlagen"); }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _all = new List<SmodInstalledRow>();
                if (mods is not null)
                {
                    foreach (var m in mods.OrderBy(m => m.Manifest?.Name ?? m.ModReference,
                                                   StringComparer.CurrentCultureIgnoreCase))
                        _all.Add(new SmodInstalledRow(m));
                    var totalBytes = _all.Sum(r => r.Source.DirSizeBytes);
                    Summary = _all.Count == 0
                        ? "Keine Mods in FactoryGame/Mods."
                        : $"{_all.Count} Mods · {totalBytes / 1024.0 / 1024.0:F1} MB";
                }
                else
                {
                    Summary = $"Fehler beim Lesen: {error}";
                }
                ApplyFilter();
                _ = EnrichRowsAsync(_all.ToArray());
            });
        });
    }

    private void ApplyFilter()
    {
        var q = SearchText?.Trim() ?? "";
        Mods.Clear();
        foreach (var row in _all)
        {
            if (q.Length > 0)
            {
                bool hit = row.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Source.ModReference.Contains(q, StringComparison.OrdinalIgnoreCase);
                if (!hit) continue;
            }
            Mods.Add(row);
        }
    }

    private async Task EnrichRowsAsync(SmodInstalledRow[] rows)
    {
        foreach (var row in rows)
        {
            var modRef = row.Source.Manifest?.ModReference;
            if (string.IsNullOrWhiteSpace(modRef)) continue;
            try
            {
                var detail = await _api.GetModDetailAsync(modRef);
                if (detail is null) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    row.Authors = detail.AuthorsDisplay;
                    row.ShortDescription = detail.ShortDescription;
                });
                if (!string.IsNullOrEmpty(detail.Logo))
                    await LoadCoverAsync(row, detail.Logo, detail.Id);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Installed-Enrichment fehlgeschlagen für {Mod}", modRef);
            }
            try { await Task.Delay(250); } catch { break; }
        }
    }

    private async Task LoadCoverAsync(SmodInstalledRow row, string url, string modId)
    {
        using var http = _host.CreateHttpClient("ficsit-covers");
        var bmp = await FicsitCoverLoader.LoadAsync(http, url, modId, _paths.FicsitCoverDir);
        if (bmp is null) return;
        await Dispatcher.UIThread.InvokeAsync(() => row.Cover = bmp);
    }

    [RelayCommand]
    private async Task UninstallAsync(SmodInstalledRow? row)
    {
        if (row is null) return;
        bool ok = await _host.Dialogs.ConfirmAsync(
            "Mod deinstallieren",
            $"„{row.DisplayName}\" wirklich löschen? Der Ordner {row.Source.ModDir} wird komplett entfernt.",
            okLabel: "Löschen", cancelLabel: "Abbrechen");
        if (!ok) return;
        try
        {
            _installer.Uninstall(row.Source);
            _host.Notifications.Notify($"Deinstalliert: {row.Source.ModReference}",
                NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Satisfactory Uninstall fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void ShowDetail(SmodInstalledRow? row)
    {
        if (row is null) return;
        var modRef = row.Source.Manifest?.ModReference ?? row.Source.ModReference;
        if (string.IsNullOrWhiteSpace(modRef))
        {
            _host.Notifications.Notify(
                "Kein mod_reference verfügbar — kann Detail nicht öffnen.",
                NotificationLevel.Info);
            return;
        }
        var vm = new ModDetailViewModel(modRef, _api, _installer, _downloadBus, _host,
            initialTitle: row.DisplayName,
            initialVersion: row.Source.Manifest?.Version,
            initialAuthors: row.Authors,
            initialShortDescription: row.ShortDescription,
            initialCover: row.Cover);
        var window = new ModDetailWindow { DataContext = vm };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) window.Show(owner); else window.Show();
    }

    [RelayCommand]
    private void OpenModsFolder() => _host.Shell.OpenDirectory(ModsDir);

    [RelayCommand]
    private void OpenModDir(SmodInstalledRow? row)
    {
        if (row is null) return;
        _host.Shell.OpenDirectory(row.Source.ModDir);
    }

    [ObservableProperty] private bool _isCheckingUpdates;

    /// <summary>Prüft für jeden installierten Mod ob ficsit eine neuere
    /// Version anbietet. Throttled 250 ms pro Mod. Setzt <c>HasUpdate</c> +
    /// <c>LatestVersion</c> auf jeder Row wo Update verfügbar. Kann nur
    /// laufen wenn Row eine <c>ModReference</c> hat (aus .uplugin).
    /// Analog LS25-<c>InstalledModsViewModel.CheckUpdatesAsync</c>.</summary>
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdates) return;
        IsCheckingUpdates = true;
        try
        {
            var rows = _all.ToList();
            int checkedCount = 0, updatedCount = 0;
            foreach (var row in rows)
            {
                var modRef = row.Source.Manifest?.ModReference;
                var installedVersion = row.Source.Manifest?.Version;
                if (string.IsNullOrWhiteSpace(modRef) || string.IsNullOrWhiteSpace(installedVersion))
                    continue;

                checkedCount++;
                Summary = $"Updates prüfen: {checkedCount} · {row.DisplayName}";
                try
                {
                    var detail = await _api.GetModDetailAsync(modRef);
                    var latest = detail?.LatestVersion;
                    if (latest is null || string.IsNullOrWhiteSpace(latest.Version)) continue;
                    if (IsVersionNewer(latest.Version, installedVersion))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => row.SetUpdateAvailable(latest.Version));
                        updatedCount++;
                        Log.Info("Update verfügbar {Mod}: {Old} → {New}",
                            row.DisplayName, installedVersion, latest.Version);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Update-Check für {Mod} fehlgeschlagen", modRef);
                }
                try { await Task.Delay(250); } catch { break; }
            }
            Summary = updatedCount > 0
                ? $"Updates gefunden: {updatedCount} von {checkedCount} geprüften Mods."
                : $"Keine Updates. {checkedCount} Mods geprüft.";
            _host.Notifications.Notify(Summary,
                updatedCount > 0 ? NotificationLevel.Success : NotificationLevel.Info);
        }
        finally { IsCheckingUpdates = false; }
    }

    /// <summary>Führt Update aus: neue Version downloaden, alte Mod-Ordner
    /// löschen, neue installieren. ModReference bleibt gleich, also überschreibt
    /// SmodInstallService.Install den Ordner mit overwrite=true.</summary>
    [RelayCommand]
    private async Task UpdateModAsync(SmodInstalledRow? row)
    {
        if (row is null || !row.HasUpdate) return;
        var modRef = row.Source.Manifest?.ModReference;
        if (string.IsNullOrWhiteSpace(modRef))
        {
            _host.Notifications.Notify("Kein ModReference — kann Update nicht auflösen.",
                NotificationLevel.Warning);
            return;
        }

        using var scope = _host.BeginProgress($"Update: {row.DisplayName}");
        scope.Report(0, "Detail laden …");
        try
        {
            var detail = await _api.GetModDetailAsync(modRef);
            var latest = detail?.LatestVersion;
            if (detail is null || latest is null || string.IsNullOrWhiteSpace(latest.Link))
            {
                _host.Notifications.Notify(
                    $"Keine Download-Version für {row.DisplayName} bei ficsit.",
                    NotificationLevel.Warning);
                return;
            }

            scope.Report(0, $"Download v{latest.Version} …");
            using var http = _host.CreateHttpClient("ficsit-download");
            var progress = new Progress<double>(f =>
                scope.Report(f, $"{row.DisplayName} v{latest.Version} · {(int)(f * 100)}%"));
            var fileName = $"{detail.ModReference}-{latest.Version}.smod";
            var smodPath = await _installer.DownloadSmodAsync(http, latest.Link,
                fileName, overwrite: true, progress);
            _downloadBus.RaiseDownloadsChanged(Path.GetFileName(smodPath));

            scope.Report(1.0, "Install …");
            _installer.Install(smodPath, overwrite: true);
            _host.Notifications.Notify(
                $"Update installiert: {row.DisplayName} → v{latest.Version}",
                NotificationLevel.Success);
            _downloadBus.RaiseModInstalled(modRef);
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update fehlgeschlagen für {Mod}", modRef);
            _host.Notifications.Notify($"Update-Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    private static bool IsVersionNewer(string candidateVersion, string installedVersion)
    {
        // ficsit-SemVer: kann suffixes haben (z.B. "1.2.3-beta"). Für den
        // Compare nur den numerischen Prefix nutzen.
        var candidate = StripSuffix(candidateVersion.TrimStart('v'));
        var installed = StripSuffix(installedVersion.TrimStart('v'));
        if (!Version.TryParse(candidate, out var cV)) return false;
        if (!Version.TryParse(installed, out var iV)) return false;
        return cV > iV;

        static string StripSuffix(string s)
        {
            var i = s.IndexOfAny(new[] { '-', '+' });
            return i > 0 ? s.Substring(0, i) : s;
        }
    }

    public void Dispose() => _watcher?.Dispose();
}

public sealed partial class SmodInstalledRow : ObservableObject
{
    public InstalledSmodMod Source { get; }
    public SmodInstalledRow(InstalledSmodMod source) => Source = source;

    public string DisplayName => Source.Manifest?.Name is { Length: > 0 } n ? n : Source.ModReference;
    public string ModReference => Source.ModReference;
    public string Version => Source.Manifest?.Version is { Length: > 0 } v ? $"v{v}" : "";
    public string Size => Source.DirSizeBytes < 1024 * 1024
        ? $"{Source.DirSizeBytes / 1024.0:F0} KB"
        : $"{Source.DirSizeBytes / 1024.0 / 1024.0:F1} MB";
    public string InstalledText => Source.InstalledUtc.ToLocalTime().ToString("g");
    public bool HasReadError => !string.IsNullOrWhiteSpace(Source.ReadError);
    public string ReadErrorText => Source.ReadError ?? "";

    [ObservableProperty] private string? _authors;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortDescription))]
    private string? _shortDescription;
    public bool HasShortDescription => !string.IsNullOrWhiteSpace(ShortDescription);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private Bitmap? _cover;
    public bool HasCover => Cover is not null;

    /// <summary>Wird von <c>CheckUpdatesAsync</c> gesetzt wenn ficsit eine
    /// neuere Version anbietet als das lokale Manifest. Steuert das
    /// Update-Badge + den ⬆ Update-Button in der Row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateBadgeText))]
    private bool _hasUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateBadgeText))]
    private string? _latestVersion;

    public string UpdateBadgeText =>
        HasUpdate && LatestVersion is not null ? $"⬆ Update v{LatestVersion}" : "";

    public void SetUpdateAvailable(string catalogVersion)
    {
        LatestVersion = catalogVersion;
        HasUpdate = true;
    }
}
