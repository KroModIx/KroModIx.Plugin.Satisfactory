using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>ficsit-Katalog-Tab. Zeigt Mods aus dem 24 h-Cache im Kroste-Card-
/// Look. Direct-Download in den Plugin-Downloads-Ordner, Detail-Dialog via
/// Doppelklick oder „🔍 Details". Analog zu Icarus-NexusView.</summary>
public sealed class CatalogView : UserControl
{
    public CatalogView()
    {
        var refreshBtn = new Button { Content = "↺  Aktualisieren" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(CatalogViewModel.RefreshCommand)));

        var openDownloadsBtn = new Button { Content = "📂  Downloads-Ordner" };
        openDownloadsBtn.Bind(Button.CommandProperty, new Binding(nameof(CatalogViewModel.OpenDownloadsFolderCommand)));

        var searchBox = new TextBox
        {
            [!TextBox.PlaceholderTextProperty] = new Binding { Source = "Katalog filtern (Name/ModReference/Beschreibung) …" },
        };
        searchBox.Bind(TextBox.TextProperty, new Binding(nameof(CatalogViewModel.SearchText))
        { Mode = BindingMode.TwoWay });

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetColumn(refreshBtn, 0);
        Grid.SetColumn(openDownloadsBtn, 1);
        Grid.SetColumn(searchBox, 2);
        refreshBtn.Margin = new Thickness(0, 0, 6, 0);
        openDownloadsBtn.Margin = new Thickness(0, 0, 12, 0);
        toolbar.Children.Add(refreshBtn);
        toolbar.Children.Add(openDownloadsBtn);
        toolbar.Children.Add(searchBox);

        var status = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogViewModel.Status)));

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(CatalogViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<CatalogRow>((row, _) => row is null ? null : BuildRowTemplate(), true);
        list.DoubleTapped += (_, _) =>
        {
            if (DataContext is CatalogViewModel vm && list.SelectedItem is CatalogRow row)
                vm.ShowDetailCommand.Execute(row);
        };

        Content = new DockPanel
        {
            Margin = new Thickness(20, 16, 20, 14),
            Children =
            {
                WithDock(toolbar, Dock.Top),
                WithDock(status, Dock.Bottom),
                list,
            },
        };
    }

    private static Control BuildRowTemplate()
    {
        var coverFrame = new Border
        {
            Width = 100, Height = 100,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
        };
        var coverPanel = new Panel();
        var coverFallback = new TextBlock
        {
            Text = "🏭", FontSize = 36,
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
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(CatalogRow.Cover)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.Name)));

        var modRef = new TextBlock { FontSize = 11 };
        modRef.Classes.Add("muted");
        modRef.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.ModReference)));

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        void AddMuted(Binding b)
        {
            var t = new TextBlock(); t.Classes.Add("muted");
            t.Bind(TextBlock.TextProperty, b);
            meta.Children.Add(t);
        }
        AddMuted(new Binding(nameof(CatalogRow.DownloadsText)));
        var sep1 = new TextBlock { Text = "·" }; sep1.Classes.Add("muted"); meta.Children.Add(sep1);
        AddMuted(new Binding(nameof(CatalogRow.ViewsText)));
        var sep2 = new TextBlock { Text = "·" }; sep2.Classes.Add("muted"); meta.Children.Add(sep2);
        AddMuted(new Binding(nameof(CatalogRow.PopularityText)));
        var sep3 = new TextBlock { Text = "·" }; sep3.Classes.Add("muted"); meta.Children.Add(sep3);
        AddMuted(new Binding(nameof(CatalogRow.UpdatedText)));

        var shortDesc = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        shortDesc.Classes.Add("secondary");
        shortDesc.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.ShortDescription)));
        shortDesc.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(CatalogRow.HasShortDescription)));

        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, modRef, meta, shortDesc },
        };

        var downloadBtn = new Button { Content = "⬇  Download" };
        downloadBtn.Classes.Add("accent");
        BindRowCommand(downloadBtn, nameof(CatalogViewModel.DownloadRowCommand));
        ToolTip.SetTip(downloadBtn, "Direct-Download der neuesten .smod in den Downloads-Ordner");

        var detailBtn = new Button { Content = "🔍  Details" };
        BindRowCommand(detailBtn, nameof(CatalogViewModel.ShowDetailCommand));

        var openBtn = new Button { Content = "↗  ficsit öffnen" };
        openBtn.Classes.Add("ghost");
        BindRowCommand(openBtn, nameof(CatalogViewModel.OpenRowInBrowserCommand));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { downloadBtn, detailBtn, openBtn },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(coverFrame);
        grid.Children.Add(textStack);
        grid.Children.Add(actions);

        var card = new Border { Margin = new Thickness(0, 0, 0, 8), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static void BindRowCommand(Button btn, string commandName)
    {
        btn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + commandName,
        });
        btn.Bind(Button.CommandParameterProperty, new Binding("."));
    }

    private static Control WithDock(Control c, Dock dock)
    {
        DockPanel.SetDock(c, dock);
        return c;
    }
}
