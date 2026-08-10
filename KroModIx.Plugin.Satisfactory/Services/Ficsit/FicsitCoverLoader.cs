using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Lädt ficsit-Cover-URLs (typischerweise .webp) in Avalonia-
/// <see cref="Bitmap"/>. WebP-Support via SixLabors.ImageSharp (Avalonia/Skia
/// versteht .webp nicht standardmäßig — direktes <c>new Bitmap(webpStream)</c>
/// wirft <c>ArgumentException: Unable to load bitmap</c>).
///
/// <para><b>In-Flight-Deduplication:</b> beim App-Start starten Katalog-,
/// Downloads- und Installed-VMs parallel und wollen alle dasselbe Cover für
/// denselben installierten Mod. Ohne Dedup → 3–5× paralleler Download +
/// File-Write-Contention (<c>IOException: file being used by another process</c>).
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> + <see cref="Lazy{T}"/>
/// stellt sicher dass pro <c>modId</c> nur EIN Task läuft; die anderen warten
/// auf dessen Ergebnis. Nach Abschluss bleibt das Ergebnis im Cache — nächste
/// Anfragen sind instant.</para>
/// </summary>
public static class FicsitCoverLoader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _inFlight
        = new(StringComparer.Ordinal);

    /// <summary>Lädt (mit Disk- + In-Memory-Dedup) ein Cover zu einem Mod.
    /// Aufrufer muss die Property-Zuweisung selbst auf UI-Thread machen.</summary>
    public static Task<Bitmap?> LoadAsync(HttpClient http, string url,
        string modId, string coverCacheDir)
    {
        if (string.IsNullOrEmpty(url)) return Task.FromResult<Bitmap?>(null);

        var lazy = _inFlight.GetOrAdd(modId, _ => new Lazy<Task<Bitmap?>>(
            () => LoadInternalAsync(http, url, modId, coverCacheDir),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private static async Task<Bitmap?> LoadInternalAsync(HttpClient http, string url,
        string modId, string coverCacheDir)
    {
        try
        {
            var localPngPath = Path.Combine(coverCacheDir, $"{modId}.png");
            if (!File.Exists(localPngPath))
            {
                Directory.CreateDirectory(coverCacheDir);
                var bytes = await http.GetByteArrayAsync(url);
                if (bytes.Length == 0)
                {
                    Log.Debug("Cover-Bytes leer für {Id}", modId);
                    return null;
                }
                await ConvertToPngAsync(bytes, localPngPath);
            }
            return await Task.Run(() =>
            {
                using var s = File.OpenRead(localPngPath);
                return new Bitmap(s);
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Cover-Load fehlgeschlagen: {Url}", url);
            return null;
        }
    }

    /// <summary>Konvertiert beliebiges von ImageSharp verstandenes Format
    /// (WebP, PNG, JPG, BMP, GIF, TIFF) nach PNG. Passiert off-Thread damit
    /// der Decode nicht den UI-Thread blockiert. Atomarer File-Write via
    /// <c>.tmp</c> + <c>File.Move</c> vermeidet Half-Written-PNGs beim
    /// Prozess-Abbruch mid-Download.</summary>
    private static async Task ConvertToPngAsync(byte[] source, string targetPngPath)
    {
        await Task.Run(() =>
        {
            using var image = SixLabors.ImageSharp.Image.Load(source);
            var tmp = targetPngPath + ".tmp";
            using (var output = File.Create(tmp))
                image.Save(output, new PngEncoder());
            if (File.Exists(targetPngPath)) File.Delete(targetPngPath);
            File.Move(tmp, targetPngPath);
        });
    }
}
