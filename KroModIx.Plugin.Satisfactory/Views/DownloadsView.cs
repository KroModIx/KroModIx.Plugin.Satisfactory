using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>Downloads-Tab: Kroste-Card-Look, pro Row Install + Details + Delete.
/// Auto-Refresh im VM via FileSystemWatcher + DownloadEventBus.</summary>
public sealed class DownloadsView : UserControl
{
    public DownloadsView()
    {
        var installAllBtn = new Button { Name = "InstallAllButton", Content = "📥  Alle installieren" };
        installAllBtn.Classes.Add("accent");
        installAllBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.InstallAllCommand)));
        ToolTip.SetTip(installAllBtn,
            "Installiert alle .smod-Downloads (überschreibt bestehende Versionen). Ideal nach einem Update-Batch.");

        var openBtn = new Button { Content = "📂  Downloads-Ordner öffnen" };
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.OpenDownloadsFolderCommand)));
        var refreshBtn = new Button { Content = "↺  Aktualisieren" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.RefreshCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { installAllBtn, openBtn, refreshBtn },
        };

        var pathLabel = new TextBlock { FontSize = 11, Margin = new Thickness(0, 0, 0, 8) };
        pathLabel.Classes.Add("muted");
        pathLabel.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.DownloadsDir))
        { StringFormat = "Ordner: {0}" });

        var summary = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        summary.Classes.Add("muted");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.Summary)));

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(DownloadsViewModel.Rows)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(DownloadsViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<DownloadRow>((row, _) => row is null ? null : BuildRowTemplate(), true);
        list.DoubleTapped += (_, _) =>
        {
            if (DataContext is DownloadsViewModel vm && list.SelectedItem is DownloadRow row)
                vm.ShowDetailCommand.Execute(row);
        };

        Content = new DockPanel
        {
            Margin = new Thickness(20, 16, 20, 14),
            Children =
            {
                WithDock(toolbar, Dock.Top),
                WithDock(pathLabel, Dock.Top),
                WithDock(summary, Dock.Bottom),
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
            Text = "📦", FontSize = 32,
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
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(DownloadRow.Cover)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.DisplayName)));

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        var authorTb = new TextBlock(); authorTb.Classes.Add("muted");
        authorTb.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.Author)));
        var sep1 = new TextBlock { Text = "·" }; sep1.Classes.Add("muted");
        var versionTb = new TextBlock(); versionTb.Classes.Add("muted");
        versionTb.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.ModVersion)));
        var sep2 = new TextBlock { Text = "·" }; sep2.Classes.Add("muted");
        var size = new TextBlock(); size.Classes.Add("muted");
        size.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.Size)));
        var sep3 = new TextBlock { Text = "·" }; sep3.Classes.Add("muted");
        var dl = new TextBlock(); dl.Classes.Add("muted");
        dl.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.DownloadedText)));
        meta.Children.Add(authorTb); meta.Children.Add(sep1);
        meta.Children.Add(versionTb); meta.Children.Add(sep2);
        meta.Children.Add(size); meta.Children.Add(sep3); meta.Children.Add(dl);

        var shortDesc = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        shortDesc.Classes.Add("secondary");
        shortDesc.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.ShortDescription)));
        shortDesc.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(DownloadRow.HasShortDescription)));

        var fileNameTb = new TextBlock { FontSize = 10, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        fileNameTb.Classes.Add("muted");
        fileNameTb.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.FileName)));

        var textStack = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, meta, shortDesc, fileNameTb },
        };

        var installBtn = new Button { Content = "📥  Installieren" };
        installBtn.Classes.Add("accent");
        BindRowCommand(installBtn, nameof(DownloadsViewModel.InstallRowCommand));

        var detailBtn = new Button { Content = "🔍  Details" };
        BindRowCommand(detailBtn, nameof(DownloadsViewModel.ShowDetailCommand));

        var deleteBtn = new Button { Content = "🗑  Löschen" };
        deleteBtn.Classes.Add("danger");
        BindRowCommand(deleteBtn, nameof(DownloadsViewModel.DeleteRowCommand));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { installBtn, detailBtn, deleteBtn },
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
