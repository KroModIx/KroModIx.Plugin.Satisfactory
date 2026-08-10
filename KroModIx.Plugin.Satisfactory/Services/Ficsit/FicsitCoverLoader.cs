using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Lädt ficsit-Cover-URLs (typischerweise .webp) in Avalonia-
/// <see cref="Bitmap"/>. Der Workflow:
///
/// <list type="number">
/// <item>URL herunterladen (bytes).</item>
/// <item>Falls die Bytes ein von Skia unterstütztes Format sind (PNG/JPG):
///   direkt in <see cref="Bitmap"/> laden.</item>
/// <item>Sonst (WebP): via SixLabors.ImageSharp decoden + als PNG
///   re-encoden, dann Skia-Bitmap.</item>
/// </list>
///
/// <para>Warum? Avalonia/Skia versteht WebP standardmäßig nicht — direkter
/// <c>new Bitmap(webpStream)</c> wirft <c>ArgumentException: Unable to load
/// bitmap</c>. ImageSharp ist Managed-C# und kennt WebP nativ.</para>
///
/// <para>Cache: die konvertierten PNGs liegen im gemeinsamen
/// <c>FicsitCoverDir</c>. Die Original-URL-Extension (.webp/.png/.jpg) wird
/// im Cache-Key ignoriert — wir speichern immer als .png weil das der
/// Ziel-Format ist.</para>
/// </summary>
public static class FicsitCoverLoader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>Lädt (mit Disk-Cache) ein Cover zu einem Mod. Nur der Bitmap-
    /// Decode läuft off-thread — Aufrufer muss aber die Property-Zuweisung
    /// (<c>row.Cover = bmp</c>) selbst auf UI-Thread machen.</summary>
    public static async Task<Bitmap?> LoadAsync(HttpClient http, string url,
        string modId, string coverCacheDir)
    {
        if (string.IsNullOrEmpty(url)) return null;
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
    /// der Decode nicht den UI-Thread blockiert.</summary>
    private static async Task ConvertToPngAsync(byte[] source, string targetPngPath)
    {
        await Task.Run(() =>
        {
            using var image = SixLabors.ImageSharp.Image.Load(source);
            using var output = File.Create(targetPngPath);
            image.Save(output, new PngEncoder());
        });
    }
}
