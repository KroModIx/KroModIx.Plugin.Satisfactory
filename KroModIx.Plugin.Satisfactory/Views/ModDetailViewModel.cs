using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.Satisfactory.Services;
using KroModIx.Plugin.Satisfactory.Services.Ficsit;
using Markdig;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>VM für den ficsit-Mod-Detail-Dialog. Lädt beim Öffnen das volle
/// Mod-Detail via <c>getModByIdOrReference</c> im Hintergrund, stripped die
/// Markdown/HTML-Beschreibung, bietet Direct-Download der neuesten Version
/// (<c>version.link</c>) und KI-Zusammenfassung über den Host-KI-Provider.
///
/// <para>Analog zu <c>NexusModDetailViewModel</c> (Icarus) und
/// <c>ModDetailViewModel</c> (LS25) — bewusste Struktur-Parallele.</para>
/// </summary>
public sealed partial class ModDetailViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // Markdig-Pipeline mit "AdvancedExtensions" — GitHub-Flavored-Markdown-
    // Superset (Tables, Autolinks, Task-Lists, Emphasis-Extras). ficsit-Autoren
    // nutzen typischerweise GitHub-README-Style, das deckt der Advanced-Preset ab.
    private static readonly MarkdownPipeline _markdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly string _modIdOrRef;
    private readonly string _detailUrl;
    private readonly FicsitApiClient _api;
    private readonly SmodInstallService _installer;
    private readonly DownloadEventBus _downloadBus;
    private readonly IHostServices _host;

    /// <summary>Konstruktor mit CatalogEntry als Vorbelegung — vom Katalog-Tab
    /// genutzt. Sofort-Anzeige der Katalog-Metadaten, Full-Detail nach.</summary>
    public ModDetailViewModel(FicsitCatalogEntry entry,
        FicsitApiClient api, SmodInstallService installer,
        DownloadEventBus downloadBus, IHostServices host)
        : this(entry.Id, api, installer, downloadBus, host,
               initialTitle: entry.Name,
               initialShortDescription: entry.ShortDescription,
               initialUpdated: entry.LastVersionDate.ToLocalTime().ToString("g"),
               initialDownloads: entry.Downloads)
    { }

    /// <summary>Vollständiger Constructor mit expliziten Vorbelegungs-Werten —
    /// vom Downloads- und Installed-Tab genutzt (dort andere Row-Struktur).</summary>
    public ModDetailViewModel(string modIdOrRef,
        FicsitApiClient api, SmodInstallService installer,
        DownloadEventBus downloadBus, IHostServices host,
        string? initialTitle = null,
        string? initialAuthors = null,
        string? initialShortDescription = null,
        string? initialVersion = null,
        string? initialUpdated = null,
        int initialDownloads = 0,
        Bitmap? initialCover = null)
    {
        _modIdOrRef = modIdOrRef;
        _detailUrl = $"https://ficsit.app/mod/{modIdOrRef}";
        _api = api;
        _installer = installer;
        _downloadBus = downloadBus;
        _host = host;

        Title = initialTitle ?? "";
        Authors = initialAuthors ?? "";
        ShortDescription = initialShortDescription ?? "";
        Version = initialVersion ?? "";
        UpdatedText = initialUpdated ?? "";
        DownloadsText = initialDownloads > 0 ? $"⬇ {initialDownloads:N0}" : "";
        Cover = initialCover;
        Description = Strings.T("status.detail_desc_placeholder");

        _ = LoadDetailAsync();
    }

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _authors = "";
    [ObservableProperty] private string _shortDescription = "";
    [ObservableProperty] private string _version = "";
    [ObservableProperty] private string _updatedText = "";
    [ObservableProperty] private string _downloadsText = "";
    [ObservableProperty] private string _compatibilityText = "";
    [ObservableProperty] private string _sourceUrl = "";
    [ObservableProperty] private string _description = "";
    // v0.9.0: Rich-HTML-View statt Plain-Text-TextBlock. Wird vom
    // Descriptions-Baukasten (Host v1.21+) erzeugt und im Detail-Window
    // per ContentControl.Content angezeigt. Plain-Text-Version bleibt in
    // Description fuer AI-Prompts + Loading-Placeholder.
    [ObservableProperty] private Control? _descriptionView;
    [ObservableProperty] private string _statusText = Strings.T("status.detail_loading");
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private Bitmap? _cover;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _aiSummary = "";
    public bool HasSummary => !string.IsNullOrWhiteSpace(AiSummary);

    [ObservableProperty] private bool _summaryBusy;
    [ObservableProperty] private bool _downloadBusy;

    /// <summary>Für die Anzeige-Bindings: haben wir eine Source-URL (GitHub etc.)
    /// für den „↗ Source" Button? Nicht alle Mods haben eine öffentliche Source.</summary>
    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);

    partial void OnSourceUrlChanged(string value) => OnPropertyChanged(nameof(HasSourceUrl));

    private string? _downloadLink;
    private string? _downloadFileName;

    private async Task LoadDetailAsync()
    {
        try
        {
            var detail = await _api.GetModDetailAsync(_modIdOrRef);
            if (detail is null)
            {
                Description = Strings.T("status.detail_load_failed");
                StatusText = Strings.T("status.detail_load_error");
                return;
            }

            if (!string.IsNullOrWhiteSpace(detail.Name)) Title = detail.Name;
            var authorsDisplay = detail.AuthorsDisplay;
            if (!string.IsNullOrWhiteSpace(authorsDisplay)) Authors = authorsDisplay;
            if (!string.IsNullOrWhiteSpace(detail.ShortDescription)) ShortDescription = detail.ShortDescription;
            SourceUrl = detail.SourceUrl;
            DownloadsText = detail.Downloads > 0 ? $"⬇ {detail.Downloads:N0}" : "";
            UpdatedText = detail.LastVersionDate.ToLocalTime().ToString("g");
            CompatibilityText = FormatCompatibility(detail.Compatibility);

            var fullSrc = detail.FullDescription ?? "";
            Description = HtmlStrip.ToPlainText(fullSrc);
            if (string.IsNullOrWhiteSpace(Description))
                Description = string.IsNullOrWhiteSpace(detail.ShortDescription)
                    ? Strings.T("status.detail_no_desc")
                    : detail.ShortDescription;

            // Rich-HTML-View parallel: Markdown erst zu HTML (Markdig),
            // dann durch Host-Parser (BBCode-Fallback + Kroste-CSS +
            // Avalonia-HtmlPanel). Control-Instanziierung MUSS auf UI-Thread
            // laufen (Skia-Thread-Affinity, siehe Contracts v1.21 Skill).
            if (!string.IsNullOrWhiteSpace(fullSrc))
            {
                try
                {
                    var htmlFromMd = Markdown.ToHtml(fullSrc, _markdownPipeline);
                    var richHtml = _host.Descriptions.ToHtml(htmlFromMd);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DescriptionView = _host.Descriptions.CreateRichView(richHtml);
                    });
                }
                catch (Exception rex)
                {
                    Log.Debug(rex, "Rich-HTML-Rendering fehlgeschlagen fuer {Mod} — Plain-Text-Fallback greift", _modIdOrRef);
                    DescriptionView = null;
                }
            }
            else
            {
                DescriptionView = null;
            }

            var latest = detail.LatestVersion;
            if (latest is not null)
            {
                Version = latest.Version.StartsWith("v") ? latest.Version : "v" + latest.Version;
                _downloadLink = latest.Link;
                _downloadFileName = $"{detail.ModReference}-{latest.Version}.smod";
                StatusText = string.Format(Strings.T("detail.status_prefix"), Version, detail.ModReference);
            }
            else
            {
                StatusText = Strings.T("status.no_version");
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Detail-Load fehlgeschlagen für {Mod}", _modIdOrRef);
            Description = Strings.T("status.error_prefix") + ex.Message;
            StatusText = Strings.T("status.detail_load_error");
        }
        finally { IsLoading = false; }
    }

    private static string FormatCompatibility(FicsitCompatibility? c)
    {
        if (c is null) return "";
        var ea = c.EA?.State ?? "?";
        var exp = c.EXP?.State ?? "?";
        return $"EA: {ea} · EXP: {exp}";
    }

    [RelayCommand]
    private void OpenInBrowser() => _host.Shell.OpenExternalUrl(_detailUrl);

    [RelayCommand]
    private void OpenSource()
    {
        if (!string.IsNullOrWhiteSpace(SourceUrl))
            _host.Shell.OpenExternalUrl(SourceUrl);
    }

    /// <summary>Direct-Download der neuesten Version aus dem Detail-Dialog.
    /// Kein OAuth nötig — ficsit-API liefert den Link öffentlich. Nach Erfolg
    /// feuert <see cref="DownloadEventBus.DownloadsChanged"/> → Downloads-Tab
    /// aktualisiert sich automatisch.</summary>
    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(_downloadLink))
        {
            _host.Notifications.Notify(Strings.T("notify.no_download_link"),
                NotificationLevel.Warning);
            return;
        }
        DownloadBusy = true;
        using var scope = _host.BeginProgress(string.Format(Strings.T("progress.ficsit_prefix"), Title));
        scope.Report(0, Strings.T("progress.download_start"));
        try
        {
            using var http = _host.CreateHttpClient("ficsit-download");
            var progress = new Progress<double>(f => scope.Report(f, string.Format(Strings.T("progress.download_file"), _downloadFileName, (int)(f * 100))));
            var target = await _installer.DownloadSmodAsync(http, _downloadLink!,
                _downloadFileName ?? $"{_modIdOrRef}.smod", overwrite: false, progress);
            _host.Notifications.Notify(Strings.T("notify.downloaded_prefix") + Path.GetFileName(target),
                NotificationLevel.Success);
            _downloadBus.RaiseDownloadsChanged(Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Detail-Download fehlgeschlagen für {Mod}", _modIdOrRef);
            _host.Notifications.Notify(Strings.T("notify.download_error_prefix") + ex.Message, NotificationLevel.Error);
        }
        finally { DownloadBusy = false; }
    }

    [RelayCommand]
    private async Task SummarizeAsync()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(Description))
        {
            _host.Notifications.Notify(Strings.T("notify.detail_wait"), NotificationLevel.Info);
            return;
        }
        if (!await _host.Ai.IsAvailableAsync())
        {
            _host.Notifications.Notify(
                Strings.T("notify.ai_unavailable"),
                NotificationLevel.Warning);
            return;
        }
        SummaryBusy = true;
        AiSummary = string.Format(Strings.T("detail.ai_running_prefix"), _host.Ai.ProviderInfo);
        try
        {
            var systemPrompt = Strings.T("ai.prompt.summary_system");
            var userPrompt = $"Titel: {Title}\nAutor(en): {Authors}\n\nBeschreibung:\n{Description}";
            var answer = await _host.Ai.CompleteAsync(systemPrompt, userPrompt);
            AiSummary = string.IsNullOrWhiteSpace(answer)
                ? Strings.T("detail.ai_no_answer")
                : answer;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Summarize fehlgeschlagen für {Mod}", _modIdOrRef);
            AiSummary = Strings.T("status.error_prefix") + ex.Message;
        }
        finally { SummaryBusy = false; }
    }
}
