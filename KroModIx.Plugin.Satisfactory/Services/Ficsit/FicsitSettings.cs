namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Plugin-lokale ficsit-Konfiguration. Aktuell nur Katalog-Refresh-
/// Intervall — kein API-Key nötig (ficsit-API ist offen für Read-Queries).
/// Kann in v0.2+ um Filter (nur SML-kompatible, min. Popularity, …) erweitert
/// werden. Analog zu <c>NexusSettings</c>/<c>Ls25Settings</c>.</summary>
public sealed class FicsitSettings
{
    /// <summary>Nach wie vielen Stunden gilt der Katalog-Cache als stale
    /// und wird beim nächsten Zugriff neu vom API gepullt. Default 24 h.</summary>
    public int CatalogRefreshHours { get; set; } = 24;

    /// <summary>Sortier-Feld für die initiale Katalog-Anzeige. Werte:
    /// <c>popularity</c>, <c>hotness</c>, <c>downloads</c>, <c>views</c>,
    /// <c>last_version_date</c>, <c>created_at</c>.</summary>
    public string DefaultSort { get; set; } = "popularity";
}
