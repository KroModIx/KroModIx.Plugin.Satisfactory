using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Persistente Zählung der installierten Mods mit verfügbarem
/// Update (aus <c>CheckUpdatesAsync</c>). Wird vom
/// <see cref="SatisfactoryPlugin.GetPendingUpdatesAsync"/> gelesen und in
/// den Sidebar-Kachel-Badge eingerechnet. Persistenz in
/// <c>&lt;PluginCacheDir&gt;/installed-updates.json</c> — damit der Badge
/// beim App-Start sofort sichtbar ist, ohne dass der User erst „Updates
/// prüfen" klicken muss.
///
/// <para>Analog zu den bereits existierenden Katalog-Trackern
/// (<c>FicsitUpdateTracker</c> zählt neue Katalog-Einträge; dieser hier
/// zählt Updates für die installierten Mods) — zwei separate Signale, im
/// <see cref="IUpdateNotifier"/>-Return kombiniert.</para>
/// </summary>
public sealed class InstalledUpdatesTracker
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly string _cachePath;
    private Payload _state;

    public InstalledUpdatesTracker(SatisfactoryPaths paths)
    {
        var cacheDir = paths.FicsitCacheDir;
        Directory.CreateDirectory(cacheDir);
        _cachePath = Path.Combine(cacheDir, "installed-updates.json");
        _state = Load() ?? new Payload();
    }

    /// <summary>Anzahl installierter Mods für die ficsit eine neuere
    /// Version anbietet (letzter Check via <c>CheckUpdatesAsync</c>).</summary>
    public int PendingCount => _state.Count;

    /// <summary>Human-readable Summary — geht in <see cref="GameUpdateInfo.Summary"/>.</summary>
    public string Summary => _state.Summary ?? "";

    /// <summary>Wann der letzte Check lief — für „Zuletzt geprüft"-Anzeige (v0.3+).</summary>
    public DateTime? LastCheckedUtc => _state.LastCheckedUtc;

    /// <summary>Vom ViewModel nach jedem <c>CheckUpdatesAsync</c>-Run
    /// aufgerufen. Persistiert atomar (tmp + Move).</summary>
    public void SetPending(int count, string summary)
    {
        _state = new Payload
        {
            Count = count,
            Summary = summary,
            LastCheckedUtc = DateTime.UtcNow,
        };
        Save();
    }

    private Payload? Load()
    {
        try
        {
            if (!File.Exists(_cachePath)) return null;
            return JsonSerializer.Deserialize<Payload>(File.ReadAllText(_cachePath));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "InstalledUpdatesTracker-Load fehlgeschlagen");
            return null;
        }
    }

    private void Save()
    {
        try
        {
            var tmp = _cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_state));
            // Move mit overwrite — Delete-dann-Move hinterlaesst bei einem
            // Crash dazwischen gar keine Datei.
            File.Move(tmp, _cachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "InstalledUpdatesTracker-Save fehlgeschlagen");
        }
    }

    private sealed class Payload
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("lastCheckedUtc")] public DateTime? LastCheckedUtc { get; set; }
    }
}
