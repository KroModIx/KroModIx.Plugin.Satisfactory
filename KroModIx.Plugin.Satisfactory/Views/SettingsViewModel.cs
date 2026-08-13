using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.Satisfactory.Services;
using KroModIx.Plugin.Satisfactory.Services.Ficsit;

namespace KroModIx.Plugin.Satisfactory.Views;

/// <summary>Minimal-Settings für v0.1.0: nur Katalog-Refresh-Intervall +
/// Info-Text zur ficsit-API. In v0.2 kann hier Enable/Disable pro Mod-Profil
/// (SMM-Style) landen.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly FicsitSettingsService _settings;
    private readonly IHostServices _host;

    public SettingsViewModel(FicsitSettingsService settings, IHostServices host)
    {
        _settings = settings;
        _host = host;
        CatalogRefreshHours = _settings.Current.CatalogRefreshHours;
        DefaultSort = _settings.Current.DefaultSort;
    }

    [ObservableProperty] private int _catalogRefreshHours;
    [ObservableProperty] private string _defaultSort = "popularity";
    [ObservableProperty] private string _statusText = "";

    [RelayCommand]
    private void Save()
    {
        _settings.Save(new FicsitSettings
        {
            CatalogRefreshHours = CatalogRefreshHours,
            DefaultSort = DefaultSort,
        });
        StatusText = Strings.T("settings.saved");
        _host.Notifications.Notify(Strings.T("notify.settings_saved"),
            NotificationLevel.Success);
    }

    [RelayCommand]
    private void OpenFicsit()
        => _host.Shell.OpenExternalUrl("https://ficsit.app");

    [RelayCommand]
    private void OpenDocs()
        => _host.Shell.OpenExternalUrl("https://docs.ficsit.app/satisfactory-modding/latest/index.html");
}
