using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Lädt den ficsit-Katalog (paginated, ~100 pro Request) und cacht
/// das Ergebnis als JSON auf Disk. Age-Check: nach 24 h wird beim nächsten
/// Zugriff im Hintergrund neu vom API gepullt. Analog zu
/// <c>NexusCatalogService</c> im Icarus-Plugin, aber ohne Rate-Limit-Sorgen
/// (ficsit-API hat aktuell kein hartes Limit für Read-Queries).</summary>
public sealed class FicsitCatalogService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const int PageSize = 100;
    private const int MaxPages = 20; // 2000 Mods hart-gedeckelt — ficsit hatte 2026-08 ca. 1200

    private readonly FicsitApiClient _api;
    private readonly FicsitSettingsService _settings;
    private readonly SatisfactoryPaths _paths;

    public FicsitCatalogService(FicsitApiClient api, FicsitSettingsService settings,
        SatisfactoryPaths paths)
    {
        _api = api;
        _settings = settings;
        _paths = paths;
    }

    /// <summary>Lädt Snapshot — bevorzugt aus Cache wenn frisch, sonst Netz.
    /// <paramref name="forceRefresh"/> ignoriert das Alter und pullt immer.</summary>
    public async Task<FicsitCatalogSnapshot> LoadAsync(bool forceRefresh,
        CancellationToken ct = default)
    {
        var maxAge = TimeSpan.FromHours(_settings.Current.CatalogRefreshHours);
        var cached = TryLoadFromDisk();
        if (!forceRefresh && cached is not null &&
            DateTime.UtcNow - cached.SavedUtc < maxAge)
        {
            Log.Info("ficsit-Katalog aus Cache: {Count} Einträge, Alter {AgeHours:F1} h",
                cached.Entries.Count, (DateTime.UtcNow - cached.SavedUtc).TotalHours);
            return cached;
        }

        var fresh = await FetchAllAsync(ct);
        if (fresh.Entries.Count == 0 && cached is not null)
        {
            Log.Warn("ficsit-Refresh lieferte 0 Einträge — behalte Cache");
            return cached;
        }
        SaveToDisk(fresh);
        return fresh;
    }

    private async Task<FicsitCatalogSnapshot> FetchAllAsync(CancellationToken ct)
    {
        var snap = new FicsitCatalogSnapshot { SavedUtc = DateTime.UtcNow };
        for (int page = 0; page < MaxPages; page++)
        {
            var offset = page * PageSize;
            var result = await _api.GetModsAsync(PageSize, offset, ct: ct);
            if (result is null || result.Mods.Count == 0) break;
            snap.Entries.AddRange(result.Mods);
            if (snap.Entries.Count >= result.Count) break;
        }
        Log.Info("ficsit-Katalog gepullt: {Count} Einträge in {Pages} Pages",
            snap.Entries.Count, (snap.Entries.Count + PageSize - 1) / PageSize);
        return snap;
    }

    private FicsitCatalogSnapshot? TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(_paths.FicsitCatalogCachePath)) return null;
            var json = File.ReadAllText(_paths.FicsitCatalogCachePath);
            return JsonSerializer.Deserialize<FicsitCatalogSnapshot>(json);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Cache-Load fehlgeschlagen");
            return null;
        }
    }

    private void SaveToDisk(FicsitCatalogSnapshot snap)
    {
        try
        {
            Directory.CreateDirectory(_paths.FicsitCacheDir);
            var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = false });
            var tmp = _paths.FicsitCatalogCachePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _paths.FicsitCatalogCachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ficsit-Cache-Save fehlgeschlagen");
        }
    }
}
