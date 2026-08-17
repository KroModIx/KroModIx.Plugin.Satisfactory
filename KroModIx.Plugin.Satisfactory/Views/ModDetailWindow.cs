using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using KroModIx.Plugin.Satisfactory.Services;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>Detail-Fenster für einen ficsit-Mod. Custom-Chrome (Kroste-Standard),
/// Drag per Titelleiste. Layout: großes Cover links, Titel + Meta + Beschreibung
/// rechts, KI-Zusammenfassung optional, Footer mit Download-Button, ficsit-Link,
/// Source-Link (falls verfügbar), Schließen. Analog zu NexusModDetailWindow im
/// Icarus-Plugin.</summary>
public sealed class ModDetailWindow : Window
{
    public ModDetailWindow()
    {
        Title = Strings.T("detail.window_title");
        Width = 900;
        Height = 720;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this[!Window.BackgroundProperty] = new DynamicResourceExtension("KrosteBackgroundBrush");
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        CanResize = true;

        Content = BuildContent();
    }

    private DockPanel BuildContent()
    {
        var titlebar = BuildTitleBar();
        var footer = BuildFooter();
        var body = BuildBody();

        var dp = new DockPanel();
        DockPanel.SetDock(titlebar, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dp.Children.Add(titlebar);
        dp.Children.Add(footer);
        dp.Children.Add(body);
        return dp;
    }

    private Border BuildTitleBar()
    {
        var titleBlock = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ModDetailViewModel.Title)));

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 40, Height = 32,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        closeBtn.Classes.Add("chrome");
        closeBtn.Click += (_, _) => Close();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Height = 32,
        };
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(closeBtn, 1);
        grid.Children.Add(titleBlock);
        grid.Children.Add(closeBtn);

        var bar = new Border { Child = grid };
        bar[!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush");
        bar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        return bar;
    }

    private Control BuildBody()
    {
        // Cover links (240x160 — ficsit liefert typischerweise Square-Logos,
        // Border-Frame ist rechteckig, Uniform-Stretch zentriert).
        var coverFrame = new Border
        {
            Width = 240, Height = 160,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var coverPanel = new Panel();
        var coverFallback = new TextBlock
        {
            Text = "🏭", FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        coverFallback.Classes.Add("muted");
        coverPanel.Children.Add(coverFallback);
        var coverImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(ModDetailViewModel.Cover)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        // Titel + Meta oben rechts
        var title = new TextBlock { TextWrapping = TextWrapping.Wrap };
        title.Classes.Add("h1");
        title.Bind(TextBlock.TextProperty, new Binding(nameof(ModDetailViewModel.Title)));

        var metaGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 10, 0, 0),
        };
        AddMetaRow(metaGrid, 0, Strings.T("detail.meta.authors"),       nameof(ModDetailViewModel.Authors));
        AddMetaRow(metaGrid, 1, Strings.T("detail.meta.version"),       nameof(ModDetailViewModel.Version));
        AddMetaRow(metaGrid, 2, Strings.T("detail.meta.updated"),       nameof(ModDetailViewModel.UpdatedText));
        AddMetaRow(metaGrid, 3, Strings.T("detail.meta.downloads"),     nameof(ModDetailViewModel.DownloadsText));
        AddMetaRow(metaGrid, 4, Strings.T("detail.meta.compatibility"), nameof(ModDetailViewModel.CompatibilityText));

        var shortDesc = new TextBlock
        {
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        shortDesc.Classes.Add("secondary");
        shortDesc.Bind(TextBlock.TextProperty, new Binding(nameof(ModDetailViewModel.ShortDescription)));

        var topRight = new StackPanel
        {
            Margin = new Thickness(16, 0, 0, 0),
            Children = { title, metaGrid, shortDesc },
        };

        var topRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, 16),
        };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(topRight, 1);
        topRow.Children.Add(coverFrame);
        topRow.Children.Add(topRight);

        var aiCard = BuildAiSummaryCard();

        var descTitle = new TextBlock { Text = Strings.T("detail.section.description"), Margin = new Thickness(0, 8, 0, 6) };
        descTitle.Classes.Add("section-label");

        // v0.9.0: Rich-HTML-Rendering via _host.Descriptions.CreateRichView
        // (HtmlPanel mit Kroste-CSS: Bold, Italic, Farben, Bilder, Listen)
        // statt Plain-Text-TextBlock. Fallback wenn noch nicht fertig geladen
        // ODER Rich-Rendering fehlgeschlagen: Loading-TextBlock zeigt Plain-
        // Text (Description enthaelt dann Placeholder oder tatsaechlichen Text).
        var descRichHost = new ContentControl();
        descRichHost.Bind(ContentControl.ContentProperty,
            new Binding(nameof(ModDetailViewModel.DescriptionView)));

        var descLoadingFallback = new TextBlock { TextWrapping = TextWrapping.Wrap };
        descLoadingFallback.Classes.Add("muted");
        descLoadingFallback.Bind(TextBlock.TextProperty,
            new Binding(nameof(ModDetailViewModel.Description)));
        descLoadingFallback.Bind(TextBlock.IsVisibleProperty,
            new Binding(nameof(ModDetailViewModel.DescriptionView))
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<Control?, bool>(
                    c => c is null),
            });

        var descCard = new Border
        {
            Padding = new Thickness(14),
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Children = { descRichHost, descLoadingFallback } },
        };

        var scrollContent = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(20, 14, 20, 14),
            Children = { topRow, aiCard, descTitle, descCard },
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = scrollContent,
        };
        return scroll;
    }

    private static Control BuildAiSummaryCard()
    {
        var title = new TextBlock { Text = Strings.T("detail.section.ai_summary"), Margin = new Thickness(0, 0, 0, 6) };
        title.Classes.Add("section-label");
        var body = new TextBlock { TextWrapping = TextWrapping.Wrap };
        body.Bind(TextBlock.TextProperty, new Binding(nameof(ModDetailViewModel.AiSummary)));
        var card = new Border
        {
            Padding = new Thickness(14),
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteAccentSoftBrush"),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Children = { title, body } },
        };
        card.Bind(Control.IsVisibleProperty, new Binding(nameof(ModDetailViewModel.HasSummary)));
        return card;
    }

    private Control BuildFooter()
    {
        // Primär-Aktion: Direct-Download. ficsit-API liefert den Link öffentlich,
        // kein OAuth nötig — Button ist immer enabled (außer Busy).
        var downloadBtn = new Button { Content = Strings.T("btn.download_long") };
        downloadBtn.Classes.Add("accent");
        downloadBtn.Bind(Button.CommandProperty, new Binding(nameof(ModDetailViewModel.DownloadCommand)));
        downloadBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(ModDetailViewModel.DownloadBusy))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v),
        });
        ToolTip.SetTip(downloadBtn, Strings.T("tooltip.download_detail"));

        var openBtn = new Button { Content = Strings.T("btn.open_ficsit_long") };
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(ModDetailViewModel.OpenInBrowserCommand)));

        var sourceBtn = new Button { Content = Strings.T("btn.open_source") };
        sourceBtn.Classes.Add("ghost");
        sourceBtn.Bind(Button.CommandProperty, new Binding(nameof(ModDetailViewModel.OpenSourceCommand)));
        sourceBtn.Bind(Button.IsVisibleProperty, new Binding(nameof(ModDetailViewModel.HasSourceUrl)));

        var summarizeBtn = new Button { Content = Strings.T("btn.ai_summary") };
        summarizeBtn.Bind(Button.CommandProperty, new Binding(nameof(ModDetailViewModel.SummarizeCommand)));
        summarizeBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(ModDetailViewModel.SummaryBusy))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v),
        });

        var closeBtn = new Button { Content = Strings.T("btn.close") };
        closeBtn.Classes.Add("ghost");
        closeBtn.Click += (_, _) => Close();

        var busy = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        busy.Classes.Add("muted");
        busy.Bind(TextBlock.TextProperty, new Binding(nameof(ModDetailViewModel.DownloadBusy))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(v => v ? Strings.T("detail.busy_download") : Strings.T("detail.busy_ai")),
        });
        busy.Bind(TextBlock.IsVisibleProperty, new MultiBinding
        {
            Bindings =
            {
                new Binding(nameof(ModDetailViewModel.SummaryBusy)),
                new Binding(nameof(ModDetailViewModel.DownloadBusy)),
            },
            Converter = new AnyTrueConverter(),
        });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { busy, summarizeBtn, downloadBtn, sourceBtn, openBtn, closeBtn },
        };

        var bar = new Border
        {
            Padding = new Thickness(14, 10),
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            Child = row,
        };
        return bar;
    }

    private sealed class AnyTrueConverter : Avalonia.Data.Converters.IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values,
            System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            foreach (var v in values) if (v is bool b && b) return true;
            return false;
        }
    }

    private static void AddMetaRow(Grid grid, int row, string label, string bindingPath)
    {
        var l = new TextBlock
        {
            Text = label, Margin = new Thickness(0, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        l.Classes.Add("muted");
        var v = new TextBlock { Margin = new Thickness(0, 2, 0, 2), VerticalAlignment = VerticalAlignment.Center };
        v.Bind(TextBlock.TextProperty, new Binding(bindingPath));
        Grid.SetRow(l, row); Grid.SetColumn(l, 0);
        Grid.SetRow(v, row); Grid.SetColumn(v, 1);
        grid.Children.Add(l);
        grid.Children.Add(v);
    }
}
