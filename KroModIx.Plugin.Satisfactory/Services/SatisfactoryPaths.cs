using System.IO;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Zentraler Pfad-Anbieter für das Satisfactory-Plugin. Kapselt die
/// vom Host gelieferten Data- und Cache-Verzeichnisse und leitet daraus die
/// plugin-eigenen Unter-Ordner ab (Downloads/Backups/Cover-Cache/Settings).
/// Analog zu <c>IcarusPaths</c>/<c>Ls25Paths</c>.</summary>
public sealed class SatisfactoryPaths
{
    private readonly IHostServices _host;

    public SatisfactoryPaths(IHostServices host)
    {
        _host = host;
        Directory.CreateDirectory(DownloadsDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(FicsitCacheDir);
        Directory.CreateDirectory(FicsitCoverDir);
    }

    public string PluginDataDir => _host.PluginDataDir;
    public string PluginCacheDir => _host.PluginCacheDir;

    /// <summary>Wohin heruntergeladene .smod-Dateien landen (Direct-Download
    /// aus dem Katalog via ficsit-API-<c>version.link</c>).</summary>
    public string DownloadsDir => Path.Combine(_host.PluginDataDir, "downloads");

    public string BackupsDir => Path.Combine(_host.PluginDataDir, "backups");

    public string FicsitCacheDir => Path.Combine(_host.PluginCacheDir, "ficsit");
    public string FicsitCoverDir => Path.Combine(_host.PluginCacheDir, "covers");

    public string FicsitSettingsPath => Path.Combine(_host.PluginDataDir, "ficsit.json");
    public string FicsitCatalogCachePath => Path.Combine(FicsitCacheDir, "catalog.json");
    public string FicsitSeenSnapshotPath => Path.Combine(FicsitCacheDir, "seen.json");
}
