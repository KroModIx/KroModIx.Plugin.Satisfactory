using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>Minimal Settings-Tab. In v0.2 wird das um Profile (SMM-Style
/// Enable/Disable-Sets) erweitert.</summary>
public sealed class SettingsView : UserControl
{
    public SettingsView()
    {
        var title = new TextBlock { Text = "ficsit-Einstellungen", Margin = new Thickness(0, 0, 0, 12) };
        title.Classes.Add("h1");

        var info = new TextBlock
        {
            Text = "Die ficsit.app-API ist offen — kein API-Key nötig. Diese " +
                "Einstellungen steuern nur den Katalog-Cache im Plugin.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        };
        info.Classes.Add("secondary");

        var refreshLabel = new TextBlock { Text = "Katalog-Cache-Alter (Stunden):", Margin = new Thickness(0, 0, 0, 4) };
        refreshLabel.Classes.Add("section-label");
        var refreshBox = new NumericUpDown { Minimum = 1, Maximum = 168, FormatString = "0", Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        refreshBox.Bind(NumericUpDown.ValueProperty, new Binding(nameof(SettingsViewModel.CatalogRefreshHours))
        { Mode = BindingMode.TwoWay });

        var sortLabel = new TextBlock { Text = "Standard-Sortierung:", Margin = new Thickness(0, 16, 0, 4) };
        sortLabel.Classes.Add("section-label");
        var sortBox = new ComboBox
        {
            Width = 200, HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = new[] { "popularity", "hotness", "downloads", "views", "last_version_date", "created_at" },
        };
        sortBox.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(SettingsViewModel.DefaultSort))
        { Mode = BindingMode.TwoWay });

        var saveBtn = new Button { Content = "💾  Speichern", Margin = new Thickness(0, 20, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        saveBtn.Classes.Add("accent");
        saveBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.SaveCommand)));

        var statusTb = new TextBlock();
        statusTb.Classes.Add("muted");
        statusTb.Bind(TextBlock.TextProperty, new Binding(nameof(SettingsViewModel.StatusText)));

        var linksLabel = new TextBlock { Text = "Nützliche Links:", Margin = new Thickness(0, 24, 0, 8) };
        linksLabel.Classes.Add("section-label");
        var ficsitBtn = new Button { Content = "↗  ficsit.app", Margin = new Thickness(0, 0, 8, 0) };
        ficsitBtn.Classes.Add("ghost");
        ficsitBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.OpenFicsitCommand)));
        var docsBtn = new Button { Content = "📖  SMM-Docs" };
        docsBtn.Classes.Add("ghost");
        docsBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.OpenDocsCommand)));
        var linksRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { ficsitBtn, docsBtn } };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(20, 16, 20, 14),
                Children = { title, info, refreshLabel, refreshBox, sortLabel, sortBox, saveBtn, statusTb, linksLabel, linksRow },
            },
        };
    }
}
