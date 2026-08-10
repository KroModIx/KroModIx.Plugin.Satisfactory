using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.Satisfactory.Services.Ficsit;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Prüft für jeden installierten Mod ob ficsit eine neuere Version
/// anbietet. Wird von zwei Callsites konsumiert:
///
/// <list type="number">
/// <item><b>User-Klick</b> in <c>InstalledSmodsViewModel.CheckUpdatesAsync</c> —
///   mit <paramref name="onUpdateFound"/>-Callback der die Row per
///   <c>SetUpdateAvailable</c> markiert und <paramref name="onProgress"/> für
///   die Summary-Zeile.</item>
/// <item><b>Auto-Check beim App-Start</b> in <c>SatisfactoryPlugin.InitializeAsync</c> —
///   ohne Callbacks, nur um <see cref="InstalledUpdatesTracker"/> zu füttern
///   damit der Sidebar-Kachel-Badge sofort sichtbar ist.</item>
/// </list>
///
/// <para>Beide Wege schreiben am Ende in denselben <see cref="InstalledUpdatesTracker"/>
/// — dadurch bleibt der Badge korrekt egal wer den Check triggert.</para></summary>
public sealed class InstalledUpdatesChecker
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SmodInstallService _installer;
    private readonly FicsitApiClient _api;
    private readonly InstalledUpdatesTracker _tracker;
    private readonly IHostServices _host;

    public InstalledUpdatesChecker(SmodInstallService installer, FicsitApiClient api,
        InstalledUpdatesTracker tracker, IHostServices host)
    {
        _installer = installer;
        _api = api;
        _tracker = tracker;
        _host = host;
    }

    /// <summary>Ausführung des Checks. Loopt über alle Mods mit ModReference,
    /// vergleicht Manifest-Version mit ficsit <c>latest_version</c>, throttled
    /// 250 ms zwischen API-Calls. Schreibt das Ergebnis in den Tracker und
    /// gibt die Anzahl gefundener Updates zurück.
    ///
    /// <para>Callbacks sind optional — beide null-safe. Der Checker läuft
    /// auch ohne UI (Auto-Trigger vom Plugin-Init).</para></summary>
    public async Task<int> CheckAsync(
        Action<string, string, string>? onUpdateFound = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        int checkedCount = 0, updatedCount = 0;
        var candidates = _installer.ListInstalled()
            .Where(m => !string.IsNullOrWhiteSpace(m.Manifest?.ModReference) &&
                        !string.IsNullOrWhiteSpace(m.Manifest?.Version))
            .ToList();

        foreach (var mod in candidates)
        {
            if (ct.IsCancellationRequested) break;
            var modRef = mod.Manifest!.ModReference;
            var installedVersion = mod.Manifest!.Version;
            checkedCount++;
            onProgress?.Invoke($"Updates prüfen: {checkedCount} · {mod.Manifest.Name}");
            try
            {
                var detail = await _api.GetModDetailAsync(modRef, ct);
                var latest = detail?.LatestVersion;
                if (latest is null || string.IsNullOrWhiteSpace(latest.Version)) continue;
                if (IsVersionNewer(latest.Version, installedVersion))
                {
                    onUpdateFound?.Invoke(modRef, installedVersion, latest.Version);
                    updatedCount++;
                    Log.Info("Update verfügbar {Mod}: {Old} → {New}",
                        mod.Manifest.Name, installedVersion, latest.Version);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Update-Check für {Mod} fehlgeschlagen", modRef);
            }
            try { await Task.Delay(250, ct); } catch { break; }
        }

        var summary = updatedCount > 0
            ? $"{updatedCount} Mod-Update(s) verfügbar (von {checkedCount} geprüft)"
            : "";
        _tracker.SetPending(updatedCount, summary);
        Log.Info("Satisfactory Update-Check fertig: {Updated}/{Checked}", updatedCount, checkedCount);
        return updatedCount;
    }

    private static bool IsVersionNewer(string candidate, string installed)
    {
        var c = StripSuffix(candidate.TrimStart('v'));
        var i = StripSuffix(installed.TrimStart('v'));
        if (!System.Version.TryParse(c, out var cV)) return false;
        if (!System.Version.TryParse(i, out var iV)) return false;
        return cV > iV;

        static string StripSuffix(string s)
        {
            var idx = s.IndexOfAny(new[] { '-', '+' });
            return idx > 0 ? s.Substring(0, idx) : s;
        }
    }
}
