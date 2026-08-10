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

/// <summary>Downloads-Tab: zeigt heruntergeladene .smod-Files im plugin-
/// eigenen Downloads-Ordner. Bietet Install-Button (entpackt nach
/// FactoryGame/Mods/) und Delete-Button pro Row. Auto-Refresh via
/// <see cref="DownloadEventBus.DownloadsChanged"/> UND FileSystemWatcher
/// auf dem Downloads-Ordner.
///
/// <para>Refresh läuft off-thread (Kroste-Plugin-Skill perf.md Regel 0) —
/// <see cref="SmodInstallService.ListDownloaded"/> öffnet intern jede .smod
/// für data.json-Parse. Bei 30+ Downloads wär's sonst UI-Freeze.</para></summary>
public sealed partial class DownloadsViewModel : ObservableObject, IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SmodInstallService _installer;
    private readonly DownloadEventBus _downloadBus;
    private readonly IHostServices _host;
    private readonly FicsitApiClient _api;
    private readonly FicsitSettingsService _ficsitSettings;
    private readonly SatisfactoryPaths _paths;
    private FileSystemWatcher? _watcher;

    public DownloadsViewModel(SmodInstallService installer, DownloadEventBus downloadBus,
        IHostServices host, FicsitApiClient api, FicsitSettingsService ficsitSettings,
        SatisfactoryPaths paths)
    {
        _installer = installer;
        _downloadBus = downloadBus;
        _host = host;
        _api = api;
        _ficsitSettings = ficsitSettings;
        _paths = paths;
        DownloadsDir = installer.DownloadsDir;
        RefreshCommand.Execute(null);
        SetupWatcher();

        _downloadBus.DownloadsChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => Refresh());
    }

    public string DownloadsDir { get; }

    public ObservableCollection<DownloadRow> Rows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private DownloadRow? _selected;

    public bool HasSelection => Selected is not null;

    [ObservableProperty] private string _summary = "";

    partial void OnSelectedChanged(DownloadRow? value) => OnPropertyChanged(nameof(HasSelection));

    private void SetupWatcher()
    {
        try
        {
            if (!Directory.Exists(DownloadsDir)) Directory.CreateDirectory(DownloadsDir);
            _watcher = new FileSystemWatcher(DownloadsDir, "*.smod")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Created += (_, _) => ScheduleRefresh();
            _watcher.Deleted += (_, _) => ScheduleRefresh();
            _watcher.Renamed += (_, _) => ScheduleRefresh();
            _host.Logger.Info("Satisfactory downloads watcher aktiv: {Dir}", DownloadsDir);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Satisfactory downloads watcher fehlgeschlagen");
        }
    }

    private DateTime _lastRefreshRequest = DateTime.MinValue;
    private bool _refreshPending;
    private void ScheduleRefresh()
    {
        _lastRefreshRequest = DateTime.UtcNow;
        if (_refreshPending) return;
        _refreshPending = true;
        _ = Task.Run(async () =>
        {
            while (DateTime.UtcNow - _lastRefreshRequest < TimeSpan.FromMilliseconds(500))
                await Task.Delay(200);
            _refreshPending = false;
            Dispatcher.UIThread.Post(() => Refresh());
        });
    }

    /// <summary>Off-thread Refresh (perf.md Regel 0). Enumeration + Metadata-
    /// Read pro .smod passiert im Task.Run; Row-Materialisierung auf UI-Thread.</summary>
    [RelayCommand]
    private void Refresh()
    {
        Summary = "Downloads werden gelesen …";
        _ = Task.Run(async () =>
        {
            List<DownloadedSmod>? files = null;
            string? error = null;
            try
            {
                files = _installer.ListDownloaded()
                    .OrderByDescending(d => d.DownloadedUtc).ToList();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _host.Logger.Warn(ex, "Satisfactory: Downloads-Liste konnte nicht geladen werden");
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Rows.Clear();
                if (files is not null)
                {
                    foreach (var d in files)
                        Rows.Add(new DownloadRow(d));
                    var totalBytes = Rows.Sum(r => r.Source.FileSizeBytes);
                    Summary = Rows.Count == 0
                        ? "Keine .smod-Dateien im Downloads-Ordner."
                        : $"{Rows.Count} .smod · {totalBytes / 1024.0 / 1024.0:F1} MB gesamt";
                }
                else
                {
                    Summary = $"Fehler beim Lesen: {error}";
                }
                _ = LoadCoversAsync(Rows.ToArray());
            });
        });
    }

    /// <summary>Cover-Loading pro Row — nutzt den ficsit-CDN-Cache via
    /// <c>getModByIdOrReference</c> auf Basis der <c>mod_reference</c> aus
    /// data.json. Throttled 250 ms zwischen API-Calls.</summary>
    private async Task LoadCoversAsync(DownloadRow[] rows)
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
                    row.Author = detail.AuthorsDisplay;
                    row.ShortDescription = detail.ShortDescription;
                });
                if (!string.IsNullOrEmpty(detail.Logo))
                    await LoadCoverImageAsync(row, detail.Logo, detail.Id);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ficsit-Enrichment fehlgeschlagen für {Mod}", modRef);
            }
            try { await Task.Delay(250); } catch { break; }
        }
    }

    private async Task LoadCoverImageAsync(DownloadRow row, string url, string modId)
    {
        using var http = _host.CreateHttpClient("ficsit-covers");
        var bmp = await FicsitCoverLoader.LoadAsync(http, url, modId, _paths.FicsitCoverDir);
        if (bmp is null) return;
        await Dispatcher.UIThread.InvokeAsync(() => row.Cover = bmp);
    }

    [RelayCommand]
    private void InstallRow(DownloadRow? row)
    {
        if (row is null) return;
        try
        {
            var installed = _installer.Install(row.Source.FilePath, overwrite: true);
            _host.Notifications.Notify($"Installiert: {installed.ModReference}",
                NotificationLevel.Success);
            _downloadBus.RaiseModInstalled(installed.ModReference);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Satisfactory Install-from-download fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    /// <summary>Bulk-Install aller Downloads (Skill Kernprinzip 6a).
    /// overwrite=true damit Updates den Mod-Ordner ersetzen können. Fehler
    /// pro Row werden geloggt, der Loop läuft weiter.</summary>
    [RelayCommand]
    private void InstallAll()
    {
        var rows = Rows.ToArray();
        if (rows.Length == 0)
        {
            _host.Notifications.Notify("Keine Downloads zu installieren.", NotificationLevel.Info);
            return;
        }
        using var scope = _host.BeginProgress($"Installiere {rows.Length} .smod-Downloads …");
        int done = 0, failed = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            scope.Report((double)i / rows.Length, $"Installiere {i + 1}/{rows.Length}: {row.DisplayName}");
            try
            {
                var installed = _installer.Install(row.Source.FilePath, overwrite: true);
                _downloadBus.RaiseModInstalled(installed.ModReference);
                done++;
            }
            catch (Exception ex)
            {
                _host.Logger.Warn(ex, "Satisfactory Bulk-Install fehlgeschlagen für {File}", row.FileName);
                failed++;
            }
        }
        var msg = failed == 0
            ? $"{done} .smods installiert."
            : $"{done} installiert, {failed} Fehler (siehe Log).";
        _host.Notifications.Notify(msg,
            failed == 0 ? NotificationLevel.Success : NotificationLevel.Warning);
        Refresh();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DownloadRow? row)
    {
        if (row is null) return;
        bool ok = await _host.Dialogs.ConfirmAsync(
            "Download löschen",
            $"„{row.Source.FileName}\" aus dem Downloads-Ordner löschen?",
            okLabel: "Löschen", cancelLabel: "Abbrechen");
        if (!ok) return;
        try
        {
            _installer.DeleteDownload(row.Source.FilePath);
            _host.Notifications.Notify($"Gelöscht: {row.Source.FileName}", NotificationLevel.Success);
            _downloadBus.RaiseDownloadsChanged(row.Source.FileName);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    /// <summary>Öffnet Detail-Dialog für die Row (via mod_reference aus data.json).</summary>
    [RelayCommand]
    private void ShowDetail(DownloadRow? row)
    {
        if (row is null) return;
        var modRef = row.Source.Manifest?.ModReference;
        if (string.IsNullOrWhiteSpace(modRef))
        {
            _host.Notifications.Notify(
                $"Kein mod_reference im .smod-Manifest: {row.Source.FileName}",
                NotificationLevel.Info);
            return;
        }
        var vm = new ModDetailViewModel(modRef, _api, _installer, _downloadBus, _host,
            initialTitle: row.Source.Manifest!.Name,
            initialVersion: row.Source.Manifest!.Version,
            initialShortDescription: row.ShortDescription,
            initialAuthors: row.Author,
            initialCover: row.Cover);
        var window = new ModDetailWindow { DataContext = vm };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) window.Show(owner); else window.Show();
    }

    [RelayCommand]
    private void OpenDownloadsFolder() => _host.Shell.OpenDirectory(DownloadsDir);

    public void Dispose() => _watcher?.Dispose();
}

public sealed partial class DownloadRow : ObservableObject
{
    public DownloadedSmod Source { get; }
    public DownloadRow(DownloadedSmod s) => Source = s;

    public string FileName => Source.FileName;
    public string Size => Source.FileSizeBytes < 1024 * 1024
        ? $"{Source.FileSizeBytes / 1024.0:F0} KB"
        : $"{Source.FileSizeBytes / 1024.0 / 1024.0:F1} MB";
    public string DownloadedText => Source.DownloadedUtc.ToLocalTime().ToString("g");
    public string ModName => Source.Manifest?.Name ?? Path.GetFileNameWithoutExtension(FileName);
    public string ModVersion => Source.Manifest?.Version is { Length: > 0 } v ? $"v{v}" : "";

    [ObservableProperty] private string? _author;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortDescription))]
    private string? _shortDescription;

    public bool HasShortDescription => !string.IsNullOrWhiteSpace(ShortDescription);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private Bitmap? _cover;

    public bool HasCover => Cover is not null;
    public string DisplayName => ModName;
}
