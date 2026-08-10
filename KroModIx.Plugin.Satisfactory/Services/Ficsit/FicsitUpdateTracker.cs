using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Tracked welche Katalog-Einträge der User schon gesehen hat. Basis
/// für den grünen ↑-Update-Badge auf der Sidebar-Kachel (IUpdateNotifier).
/// Persistiert eine Baseline-<c>LastVersionDate</c> pro Mod-Id — Einträge mit
/// neuerem Datum gelten als „ungesehen". Beim ersten Aufruf wird die Baseline
/// auf „jetzt" gesetzt, damit der User nicht sofort einen Sturm von 1200
/// „neuen" Mods sieht. Analog zu <c>NexusUpdateTracker</c> im Icarus-Plugin.</summary>
public sealed class FicsitUpdateTracker
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SatisfactoryPaths _paths;

    public FicsitUpdateTracker(SatisfactoryPaths paths) => _paths = paths;

    public int CountNewSince(FicsitCatalogSnapshot snapshot)
    {
        if (snapshot.Entries.Count == 0) return 0;
        var baseline = LoadBaseline();
        if (baseline is null)
        {
            // Erst-Aufruf: Baseline setzen und 0 zurückgeben — kein Badge-Sturm.
            SaveBaselineFrom(snapshot);
            return 0;
        }
        int count = 0;
        foreach (var e in snapshot.Entries)
            if (e.LastVersionDate > baseline.Value)
                count++;
        return count;
    }

    /// <summary>Vom UI beim Öffnen des Katalog-Tabs aufgerufen — markiert
    /// den aktuellen Snapshot als „gesehen".</summary>
    public void MarkSeen()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.FicsitSeenSnapshotPath)!);
            var body = new BaselinePayload { LastSeenUtc = DateTime.UtcNow };
            File.WriteAllText(_paths.FicsitSeenSnapshotPath,
                JsonSerializer.Serialize(body));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "MarkSeen fehlgeschlagen");
        }
    }

    private void SaveBaselineFrom(FicsitCatalogSnapshot snapshot)
    {
        // Baseline = neuestes LastVersionDate im aktuellen Snapshot. So sieht der
        // User beim allernächsten Sync 0 „neue" Einträge, und alles danach ist echt neu.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.FicsitSeenSnapshotPath)!);
            var latest = snapshot.Entries.Max(e => e.LastVersionDate);
            var body = new BaselinePayload { LastSeenUtc = latest };
            File.WriteAllText(_paths.FicsitSeenSnapshotPath,
                JsonSerializer.Serialize(body));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Baseline-Save fehlgeschlagen");
        }
    }

    private DateTime? LoadBaseline()
    {
        try
        {
            if (!File.Exists(_paths.FicsitSeenSnapshotPath)) return null;
            var body = JsonSerializer.Deserialize<BaselinePayload>(File.ReadAllText(_paths.FicsitSeenSnapshotPath));
            return body?.LastSeenUtc;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Baseline-Load fehlgeschlagen");
            return null;
        }
    }

    private sealed class BaselinePayload
    {
        public DateTime LastSeenUtc { get; set; }
    }
}
