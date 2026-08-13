using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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

/// <summary>Katalog-Tab-VM: lädt ficsit-Mods aus dem Katalog-Cache (24 h Age-
/// Check via <see cref="FicsitCatalogService"/>), zeigt Rows mit Cover, bietet
/// Search, Detail-Dialog, Direct-Download. Analog zu Icarus-NexusViewModel.</summary>
public sealed partial class CatalogViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly FicsitCatalogService _catalog;
    private readonly FicsitSettingsService _settings;
    private readonly FicsitApiClient _api;
    private readonly SmodInstallService _installer;
    private readonly DownloadEventBus _downloadBus;
    private readonly SatisfactoryPaths _paths;
    private readonly FicsitUpdateTracker _updateTracker;
    private readonly IHostServices _host;

    private System.Collections.Generic.List<FicsitCatalogEntry> _all = new();
    private static readonly SemaphoreSlim _coverGate = new(6, 6);

    public CatalogViewModel(FicsitCatalogService catalog, FicsitSettingsService settings,
        FicsitApiClient api, SmodInstallService installer, DownloadEventBus downloadBus,
        SatisfactoryPaths paths, FicsitUpdateTracker updateTracker, IHostServices host)
    {
        _catalog = catalog;
        _settings = settings;
        _api = api;
        _installer = installer;
        _downloadBus = downloadBus;
        _paths = paths;
        _updateTracker = updateTracker;
        _host = host;
        _ = InitializeAsync();
    }

    public ObservableCollection<CatalogRow> Rows { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _status = Strings.T("status.loading_catalog");
    [ObservableProperty] private bool _isBusy;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task InitializeAsync()
    {
        await LoadAsync(forceRefresh: false);
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        IsBusy = true;
        try
        {
            var snap = await _catalog.LoadAsync(forceRefresh);
            _all = snap.Entries.ToList();
            var ageH = (int)(DateTime.UtcNow - snap.SavedUtc).TotalHours;
            Status = string.Format(Strings.T("status.catalog_count"), snap.Entries.Count, ageH);
            ApplyFilter();
            _updateTracker.MarkSeen();
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Katalog-Load fehlgeschlagen");
            Status = Strings.T("status.catalog_load_error") + ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(forceRefresh: true);

    private void ApplyFilter()
    {
        var q = SearchText?.Trim() ?? "";
        Rows.Clear();
        var filtered = _all.AsEnumerable();
        if (q.Length > 0)
        {
            filtered = filtered.Where(e =>
                e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.ModReference.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.ShortDescription.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        // Sort by popularity desc (Default) — später konfigurierbar via SettingsView.
        filtered = filtered.OrderByDescending(e => e.Popularity);
        foreach (var e in filtered)
            Rows.Add(new CatalogRow(e));

        _ = LoadCoversAsync(Rows.ToArray());
    }

    private async Task LoadCoversAsync(CatalogRow[] rows)
    {
        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Source.Logo)) continue;
            await _coverGate.WaitAsync();
            _ = LoadOneCoverAsync(row);
        }
    }

    private async Task LoadOneCoverAsync(CatalogRow row)
    {
        try
        {
            using var http = _host.CreateHttpClient("ficsit-covers");
            var bmp = await FicsitCoverLoader.LoadAsync(http, row.Source.Logo,
                row.Source.Id, _paths.FicsitCoverDir);
            if (bmp is null) return;
            await Dispatcher.UIThread.InvokeAsync(() => row.Cover = bmp);
        }
        finally { _coverGate.Release(); }
    }

    [RelayCommand]
    private void OpenRowInBrowser(CatalogRow? row)
    {
        if (row is null) return;
        _host.Shell.OpenExternalUrl(row.Source.DetailUrl);
    }

    /// <summary>Öffnet den Detail-Dialog. Analog Icarus-NexusViewModel.ShowDetail.</summary>
    [RelayCommand]
    private void ShowDetail(CatalogRow? row)
    {
        if (row is null) return;
        var vm = new ModDetailViewModel(row.Source, _api, _installer, _downloadBus, _host);
        var window = new ModDetailWindow { DataContext = vm };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) window.Show(owner); else window.Show();
    }

    [RelayCommand]
    private void OpenDownloadsFolder() => _host.Shell.OpenDirectory(_paths.DownloadsDir);

    /// <summary>Direct-Download der neuesten Version einer Row.</summary>
    [RelayCommand]
    private async Task DownloadRowAsync(CatalogRow? row)
    {
        if (row is null) return;
        using var scope = _host.BeginProgress(string.Format(Strings.T("progress.ficsit_prefix"), row.Source.Name));
        scope.Report(0, Strings.T("progress.detail_load"));
        try
        {
            var detail = await _api.GetModDetailAsync(row.Source.Id);
            var latest = detail?.LatestVersion;
            if (detail is null || latest is null || string.IsNullOrWhiteSpace(latest.Link))
            {
                _host.Notifications.Notify(string.Format(Strings.T("notify.no_download_version"), row.Source.Name),
                    NotificationLevel.Warning);
                return;
            }
            scope.Report(0, string.Format(Strings.T("progress.download_prefix_simple"), latest.Version));
            using var http = _host.CreateHttpClient("ficsit-download");
            var progress = new Progress<double>(f => scope.Report(f, string.Format(Strings.T("progress.download_row_simple"), row.Source.Name, (int)(f * 100))));
            var fileName = $"{detail.ModReference}-{latest.Version}.smod";
            var target = await _installer.DownloadSmodAsync(http, latest.Link, fileName, overwrite: false, progress);
            _host.Notifications.Notify(Strings.T("notify.downloaded_prefix") + Path.GetFileName(target),
                NotificationLevel.Success);
            _downloadBus.RaiseDownloadsChanged(Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Download fehlgeschlagen für {Mod}", row.Source.Id);
            _host.Notifications.Notify(Strings.T("notify.download_error_prefix") + ex.Message, NotificationLevel.Error);
        }
    }
}

public sealed partial class CatalogRow : ObservableObject
{
    public FicsitCatalogEntry Source { get; }
    public CatalogRow(FicsitCatalogEntry source) => Source = source;

    public string Name => Source.Name;
    public string ModReference => Source.ModReference;
    public string ShortDescription => Source.ShortDescription;
    public bool HasShortDescription => !string.IsNullOrWhiteSpace(Source.ShortDescription);
    public string DownloadsText => Source.Downloads > 0 ? $"⬇ {Source.Downloads:N0}" : "";
    public string ViewsText => Source.Views > 0 ? $"👁 {Source.Views:N0}" : "";
    public string UpdatedText => Source.LastVersionDate.ToLocalTime().ToString("g");
    public string PopularityText => Source.Popularity > 0 ? $"⭐ {Source.Popularity:F2}" : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private Bitmap? _cover;

    public bool HasCover => Cover is not null;
}
