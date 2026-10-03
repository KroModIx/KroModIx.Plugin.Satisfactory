using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Verwaltet .smod-Files und installierte Mod-Ordner in
/// <c>&lt;InstallDir&gt;/FactoryGame/Mods/</c>. Analog zu
/// <c>PakInstallService</c> im Icarus-Plugin, aber Ordner-basiert statt File-
/// basiert (SMM-Konvention: pro Mod ein eigener Ordner mit dem
/// <c>mod_reference</c> als Namen, entpackter .smod-Inhalt).</summary>
public sealed class SmodInstallService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly string _modsDir;
    private readonly string _downloadsDir;
    private readonly SmodMetadataReader _reader;
    private readonly IArchiveService _archives;

    public SmodInstallService(string modsDir, string downloadsDir, SmodMetadataReader reader,
        IArchiveService archives)
    {
        _modsDir = modsDir;
        _downloadsDir = downloadsDir;
        _reader = reader;
        _archives = archives;
        Directory.CreateDirectory(_downloadsDir);
    }

    public string ModsDir => _modsDir;
    public string DownloadsDir => _downloadsDir;

    /// <summary>Enumeriert alle Ordner in <c>FactoryGame/Mods/</c>. Ordner ohne
    /// <c>data.json</c> werden trotzdem gelistet (mit <c>ReadError</c>), damit
    /// der User sie im UI sieht und aufräumen kann.</summary>
    public IReadOnlyList<InstalledSmodMod> ListInstalled()
    {
        if (!Directory.Exists(_modsDir))
        {
            Log.Info("Mods-Ordner existiert nicht: {Path}", _modsDir);
            return Array.Empty<InstalledSmodMod>();
        }

        var result = new List<InstalledSmodMod>();
        foreach (var dir in Directory.EnumerateDirectories(_modsDir))
        {
            var (manifest, error) = _reader.ReadFromDirectory(dir);
            var info = new DirectoryInfo(dir);
            var modRef = manifest?.ModReference;
            if (string.IsNullOrWhiteSpace(modRef)) modRef = info.Name;
            long size = 0;
            try { size = info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { /* Rechte-Probleme ignorieren, Size = 0 */ }
            result.Add(new InstalledSmodMod(
                ModDir: dir,
                ModReference: modRef!,
                DirSizeBytes: size,
                InstalledUtc: info.LastWriteTimeUtc,
                Manifest: manifest,
                ReadError: error));
        }
        return result;
    }

    /// <summary>Enumeriert alle .smod-Files im plugin-eigenen Downloads-Ordner.
    /// Metadata aus dem Cache (siehe <see cref="SmodMetadataReader"/>).</summary>
    public IReadOnlyList<DownloadedSmod> ListDownloaded()
    {
        if (!Directory.Exists(_downloadsDir)) return Array.Empty<DownloadedSmod>();
        var result = new List<DownloadedSmod>();
        foreach (var file in Directory.EnumerateFiles(_downloadsDir, "*.smod"))
        {
            var info = new FileInfo(file);
            var (manifest, error) = _reader.ReadFromZip(file);
            result.Add(new DownloadedSmod(
                FilePath: file,
                FileName: Path.GetFileName(file),
                FileSizeBytes: info.Length,
                DownloadedUtc: info.LastWriteTimeUtc,
                Manifest: manifest,
                ReadError: error));
        }
        return result;
    }

    /// <summary>Entpackt eine .smod nach <c>&lt;ModsDir&gt;/&lt;ModReference&gt;/</c>.
    /// Falls das Ziel schon existiert und <paramref name="overwrite"/> false ist,
    /// wirft eine Exception. Bei true wird das Ziel komplett gelöscht + neu
    /// entpackt (Update-Case). Der ModReference kommt aus data.json — ohne
    /// gültiges Manifest schlägt Install fehl.
    ///
    /// <para><b>Seit v0.13.0 über <c>IHostServices.Archives</c></b> (Host
    /// v1.33.0). Vorher lief das über <c>ZipFile.ExtractToDirectory</c>. Das
    /// hat <b>keine Lücke</b> gelassen — nachgemessen am 03.10.2026: ein
    /// Eintrag außerhalb des Ziels führt zu
    /// <c>IOException: Extracting Zip entry would have resulted in a file
    /// outside the specified destination directory.</c> und die Datei wird
    /// nicht geschrieben. Es bricht aber <b>mitten im Vorgang</b> ab und
    /// lässt die bereits entpackten Dateien liegen — und weil der
    /// Update-Weg das Zielverzeichnis vorher löscht, stand der Nutzer dann
    /// mit einer halb installierten Mod da, wo vorher eine ganze war,
    /// und mit einer Meldung aus dem Framework.</para>
    ///
    /// <para>Jetzt werden die Einträge <b>vor</b> dem Löschen geprüft. Ist
    /// einer unsicher, passiert gar nichts: die alte Fassung bleibt
    /// stehen.</para></summary>
    public InstalledSmodMod Install(string smodPath, bool overwrite = false)
    {
        var (manifest, error) = _reader.ReadFromZip(smodPath);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.ModReference))
            throw new InvalidDataException($"Kein gültiges Manifest in .smod: {error ?? "unbekannter Fehler"}");

        Directory.CreateDirectory(_modsDir);
        var targetDir = Path.Combine(_modsDir, manifest.ModReference);

        // Pruefen BEVOR die alte Fassung geloescht wird. Der Ordnername kommt
        // aus dem Manifest im Archiv, ist also genauso unvertrauenswuerdig
        // wie ein Eintragspfad.
        if (!_archives.TryResolveSafe(_modsDir, manifest.ModReference, out _))
            throw new InvalidDataException(
                $"Unsicherer ModReference im Manifest: {manifest.ModReference}");
        var unsicher = _archives.List(smodPath)
            .Where(e => !_archives.TryResolveSafe(targetDir, e.Path, out _))
            .ToList();
        if (unsicher.Count > 0)
        {
            Log.Warn("Ausbruchsversuch in {Smod}, {Count} Eintrag/Eintraege: {Entries}",
                smodPath, unsicher.Count, string.Join(", ", unsicher.Select(e => e.Path)));
            throw new InvalidDataException(
                $"Die .smod enthaelt {unsicher.Count} Eintrag/Eintraege, die aus dem "
                + "Mod-Ordner herausschreiben wollen — "
                + string.Join(", ", unsicher.Take(3).Select(e => e.Path))
                + (unsicher.Count > 3 ? ", …" : "")
                + ". Nicht installiert; eine vorhandene Fassung bleibt unangetastet.");
        }

        if (Directory.Exists(targetDir))
        {
            if (!overwrite)
                throw new IOException($"Mod-Ordner existiert bereits: {targetDir}");
            Directory.Delete(targetDir, recursive: true);
        }

        Log.Info("Install: {Smod} -> {Target}", smodPath, targetDir);
        _archives.Extract(smodPath, targetDir);

        var info = new DirectoryInfo(targetDir);
        long size = 0;
        try { size = info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { }
        return new InstalledSmodMod(
            ModDir: targetDir,
            ModReference: manifest.ModReference,
            DirSizeBytes: size,
            InstalledUtc: info.LastWriteTimeUtc,
            Manifest: manifest,
            ReadError: null);
    }

    /// <summary>Löscht den Mod-Ordner rekursiv.</summary>
    public void Uninstall(InstalledSmodMod mod)
    {
        if (!Directory.Exists(mod.ModDir)) return;
        Log.Info("Uninstall: {Dir}", mod.ModDir);
        Directory.Delete(mod.ModDir, recursive: true);
    }

    /// <summary>Löscht eine .smod im Downloads-Ordner + Cache-Eintrag.</summary>
    public void DeleteDownload(string smodPath)
    {
        if (!File.Exists(smodPath)) return;
        Log.Info("Delete download: {Smod}", smodPath);
        File.Delete(smodPath);
        _reader.InvalidateCache(smodPath);
    }

    /// <summary>Streamed einen .smod-Download in den Downloads-Ordner. Progress-
    /// Callback bekommt Fraction 0..1. Analog zu Icarus-<c>DownloadPakAsync</c>.
    ///
    /// <para><b>URL-Handling:</b> ficsit-API liefert <c>version.link</c> als
    /// relativen Pfad wie <c>/v1/version/&lt;id&gt;/Windows/download</c>. Wir
    /// prefixen <c>https://api.ficsit.app</c> wenn's nicht schon absolute
    /// URL ist. Auf Linux via Steam-Proton läuft die Windows-Variante
    /// (Satisfactory ist Windows-only; Proton übersetzt) — deshalb Windows-
    /// Link.</para></summary>
    public async Task<string> DownloadSmodAsync(HttpClient http, string url,
        string fileName, bool overwrite, IProgress<double>? progress = null)
    {
        Directory.CreateDirectory(_downloadsDir);
        if (!fileName.EndsWith(".smod", StringComparison.OrdinalIgnoreCase))
            fileName += ".smod";
        var target = Path.Combine(_downloadsDir, fileName);
        if (File.Exists(target) && !overwrite)
            throw new IOException($"Download existiert bereits: {fileName}");

        var absoluteUrl = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : "https://api.ficsit.app" + (url.StartsWith('/') ? url : "/" + url);

        using var resp = await http.GetAsync(absoluteUrl, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        var tmp = target + ".part";
        try
        {
            using (var input = await resp.Content.ReadAsStreamAsync())
            using (var output = File.Create(tmp))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read);
                    received += read;
                    if (total is long t && t > 0)
                        progress?.Report((double)received / t);
                }
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
            _reader.InvalidateCache(target);
            return target;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }
}
