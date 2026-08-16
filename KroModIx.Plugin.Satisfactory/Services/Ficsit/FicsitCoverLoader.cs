using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Laedt ficsit-Cover-URLs (typischerweise .webp) in Avalonia-
/// <see cref="Bitmap"/>.
///
/// <para>v0.8.0: Format-Decode + Bitmap-Instantiation macht ab jetzt der
/// zentrale Host-Baukasten <see cref="IImageDecoder"/> (Contracts v1.18+).
/// WebP/AVIF/DDS-Fallbacks + Thread-Affinity werden vom Host erledigt —
/// das Plugin kippt die Bytes einfach rein. Vorher: SixLabors.ImageSharp
/// Convert-to-PNG on-disk mit Skia-Bitmap-Ctor.</para>
///
/// <para><b>In-Flight-Deduplication:</b> beim App-Start starten Katalog-,
/// Downloads- und Installed-VMs parallel und wollen alle dasselbe Cover fuer
/// denselben installierten Mod. Ohne Dedup → 3–5× paralleler Download +
/// File-Write-Contention (<c>IOException: file being used by another process</c>).
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> + <see cref="Lazy{T}"/>
/// stellt sicher dass pro <c>modId</c> nur EIN Task laeuft; die anderen warten
/// auf dessen Ergebnis. Nach Abschluss bleibt das Ergebnis im Cache — naechste
/// Anfragen sind instant.</para>
/// </summary>
public static class FicsitCoverLoader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _inFlight
        = new(StringComparer.Ordinal);

    /// <summary>Laedt (mit Disk- + In-Memory-Dedup) ein Cover zu einem Mod.
    /// Aufrufer muss die Property-Zuweisung selbst auf UI-Thread machen.</summary>
    public static Task<Bitmap?> LoadAsync(HttpClient http, IHostServices host,
        string url, string modId, string coverCacheDir)
    {
        if (string.IsNullOrEmpty(url)) return Task.FromResult<Bitmap?>(null);

        var lazy = _inFlight.GetOrAdd(modId, _ => new Lazy<Task<Bitmap?>>(
            () => LoadInternalAsync(http, host, url, modId, coverCacheDir),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private static async Task<Bitmap?> LoadInternalAsync(HttpClient http,
        IHostServices host, string url, string modId, string coverCacheDir)
    {
        try
        {
            var localPath = Path.Combine(coverCacheDir, modId + ".img");
            byte[] bytes;
            if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
            {
                bytes = await File.ReadAllBytesAsync(localPath);
            }
            else
            {
                Directory.CreateDirectory(coverCacheDir);
                bytes = await http.GetByteArrayAsync(url);
                if (bytes.Length == 0)
                {
                    Log.Debug("Cover-Bytes leer fuer {Id}", modId);
                    return null;
                }
                // Sanity: nicht cachen wenn's kein Bild ist (Login-Wall/HTML/JSON)
                if (!host.Images.LooksLikeImage(bytes))
                {
                    Log.Debug("URL liefert kein Bild — wird nicht gecached: {Url}", url);
                    return null;
                }
                var tmp = localPath + $".tmp.{Guid.NewGuid():N}";
                await File.WriteAllBytesAsync(tmp, bytes);
                if (File.Exists(localPath)) File.Delete(localPath);
                File.Move(tmp, localPath);
            }
            return await host.Images.DecodeAsync(bytes);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Cover-Load fehlgeschlagen: {Url}", url);
            return null;
        }
    }
}
