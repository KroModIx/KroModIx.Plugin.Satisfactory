using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>Installiert-Tab. Zeigt Mod-Ordner in FactoryGame/Mods/ als Cards.
/// Cover + Meta via ficsit-Enrichment. Detail-Dialog per Doppelklick oder
/// "🔍 Details". Analog Icarus-InstalledPaksView v1.10.0.</summary>
public sealed class InstalledSmodsView : UserControl
{
    public InstalledSmodsView()
    {
        var checkUpdatesBtn = new Button { Name = "CheckUpdatesButton", Content = "🔄  Updates prüfen" };
        checkUpdatesBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledSmodsViewModel.CheckUpdatesCommand)));
        checkUpdatesBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(InstalledSmodsViewModel.IsCheckingUpdates))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v),
        });
        ToolTip.SetTip(checkUpdatesBtn,
            "Prüft für jeden installierten Mod ob ficsit eine neuere Version anbietet (throttled 250 ms pro Mod).");

        var refreshBtn = new Button { Content = "↺  Aktualisieren" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledSmodsViewModel.RefreshCommand)));
        var openFolderBtn = new Button { Content = "📂  Mods-Ordner" };
        openFolderBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledSmodsViewModel.OpenModsFolderCommand)));

        var searchBox = new TextBox
        {
            [!TextBox.PlaceholderTextProperty] = new Binding { Source = "Installierte Mods filtern …" },
        };
        searchBox.Bind(TextBox.TextProperty, new Binding(nameof(InstalledSmodsViewModel.SearchText))
        { Mode = BindingMode.TwoWay });

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"),
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetColumn(checkUpdatesBtn, 0);
        Grid.SetColumn(refreshBtn, 1);
        Grid.SetColumn(openFolderBtn, 2);
        Grid.SetColumn(searchBox, 3);
        checkUpdatesBtn.Margin = new Thickness(0, 0, 6, 0);
        refreshBtn.Margin = new Thickness(0, 0, 6, 0);
        openFolderBtn.Margin = new Thickness(0, 0, 12, 0);
        toolbar.Children.Add(checkUpdatesBtn);
        toolbar.Children.Add(refreshBtn);
        toolbar.Children.Add(openFolderBtn);
        toolbar.Children.Add(searchBox);

        var pathLabel = new TextBlock { FontSize = 11, Margin = new Thickness(0, 0, 0, 8) };
        pathLabel.Classes.Add("muted");
        pathLabel.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledSmodsViewModel.ModsDir))
        { StringFormat = "Mods: {0}" });

        var summary = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        summary.Classes.Add("muted");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledSmodsViewModel.Summary)));

        var list = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(InstalledSmodsViewModel.Mods)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(InstalledSmodsViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<SmodInstalledRow>((row, _) => row is null ? null : BuildRowTemplate(), true);
        list.DoubleTapped += (_, _) =>
        {
            if (DataContext is InstalledSmodsViewModel vm && list.SelectedItem is SmodInstalledRow row)
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
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(SmodInstalledRow.Cover)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.DisplayName)));

        // Update-Badge (Kroste-Gold auf schwarz) rechts vom Titel — nur wenn
        // CheckUpdatesAsync ein Update entdeckt hat.
        var updateBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteGoldBrush"),
        };
        var updateBadgeText = new TextBlock
        {
            FontSize = 10, FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.Black,
        };
        updateBadgeText.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.UpdateBadgeText)));
        updateBadge.Child = updateBadgeText;
        updateBadge.Bind(Border.IsVisibleProperty, new Binding(nameof(SmodInstalledRow.HasUpdate)));

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { title, updateBadge } };

        var modRef = new TextBlock { FontSize = 11 };
        modRef.Classes.Add("muted");
        modRef.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.ModReference)));

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        var authorTb = new TextBlock(); authorTb.Classes.Add("muted");
        authorTb.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.Authors)));
        var sep1 = new TextBlock { Text = "·" }; sep1.Classes.Add("muted");
        var versionTb = new TextBlock(); versionTb.Classes.Add("muted");
        versionTb.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.Version)));
        var sep2 = new TextBlock { Text = "·" }; sep2.Classes.Add("muted");
        var sizeTb = new TextBlock(); sizeTb.Classes.Add("muted");
        sizeTb.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.Size)));
        meta.Children.Add(authorTb); meta.Children.Add(sep1);
        meta.Children.Add(versionTb); meta.Children.Add(sep2);
        meta.Children.Add(sizeTb);

        var shortDesc = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        shortDesc.Classes.Add("secondary");
        shortDesc.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.ShortDescription)));
        shortDesc.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(SmodInstalledRow.HasShortDescription)));

        var errorTb = new TextBlock
        {
            FontSize = 10, Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        errorTb.Classes.Add("danger");
        errorTb.Bind(TextBlock.TextProperty, new Binding(nameof(SmodInstalledRow.ReadErrorText))
        { StringFormat = "⚠ {0}" });
        errorTb.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(SmodInstalledRow.HasReadError)));

        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { titleRow, modRef, meta, shortDesc, errorTb },
        };

        // Update-Button (Accent) nur sichtbar wenn HasUpdate.
        var updateBtn = new Button { Content = "⬆  Update" };
        updateBtn.Classes.Add("accent");
        BindRowCommand(updateBtn, nameof(InstalledSmodsViewModel.UpdateModCommand));
        updateBtn.Bind(Button.IsVisibleProperty, new Binding(nameof(SmodInstalledRow.HasUpdate)));

        var detailBtn = new Button { Content = "🔍  Details" };
        BindRowCommand(detailBtn, nameof(InstalledSmodsViewModel.ShowDetailCommand));

        var openDirBtn = new Button { Content = "📂  Ordner" };
        openDirBtn.Classes.Add("ghost");
        BindRowCommand(openDirBtn, nameof(InstalledSmodsViewModel.OpenModDirCommand));

        var uninstallBtn = new Button { Content = "🗑  Deinstallieren" };
        uninstallBtn.Classes.Add("danger");
        BindRowCommand(uninstallBtn, nameof(InstalledSmodsViewModel.UninstallCommand));

        var actions = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { updateBtn, detailBtn, openDirBtn, uninstallBtn },
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
