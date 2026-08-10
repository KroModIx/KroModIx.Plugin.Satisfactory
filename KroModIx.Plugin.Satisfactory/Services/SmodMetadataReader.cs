using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using NLog;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Liest <c>data.json</c> aus einer .smod-ZIP (das SMM-Manifest).
/// Cache: pro (Path, Mtime, Size) mit <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// und <see cref="Lazy{T}"/> — verhindert doppelte ZIP-Reads wenn mehrere VMs
/// parallel <c>ListDownloaded()</c> aufrufen (siehe Kroste-Plugin-Skill
/// perf.md Regel 0b, LS25 v1.8.2). Für installierte Mods (Ordner-Struktur,
/// keine ZIP) liest die Methode <see cref="ReadFromDirectory"/> direkt.
///
/// <para>Format-Referenz: SMM-Docs
/// <c>https://docs.ficsit.app/satisfactory-modding/latest/ForUsers/SatisfactoryModManager.html</c>
/// — data.json hat mindestens <c>mod_reference</c>, <c>name</c>, <c>version</c>,
/// <c>description</c>, <c>sml_version</c>, <c>game_version</c>, <c>objects</c>.</para>
/// </summary>
public sealed class SmodMetadataReader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<string, Lazy<CacheEntry>> _cache
        = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CacheEntry(long MtimeTicks, long Size, SmodManifest? Manifest, string? Error);

    /// <summary>Liest eine .smod (ZIP-Datei). Cache-Hit wenn (Mtime, Size)
    /// unverändert — sonst frischer ZIP-Read.</summary>
    public (SmodManifest? Manifest, string? Error) ReadFromZip(string smodPath)
    {
        FileInfo info;
        try { info = new FileInfo(smodPath); }
        catch (Exception ex)
        {
            Log.Warn(ex, "FileInfo fehlgeschlagen: {Path}", smodPath);
            return (null, ex.Message);
        }
        if (!info.Exists) return (null, "Datei nicht gefunden");

        var mtime = info.LastWriteTimeUtc.Ticks;
        var size = info.Length;

        var lazy = _cache.GetOrAdd(smodPath, _ => new Lazy<CacheEntry>(
            () => ReadFromDiskInternal(smodPath, mtime, size),
            LazyThreadSafetyMode.ExecutionAndPublication));

        var entry = lazy.Value;
        if (entry.MtimeTicks == mtime && entry.Size == size)
            return (entry.Manifest, entry.Error);

        var fresh = new Lazy<CacheEntry>(
            () => ReadFromDiskInternal(smodPath, mtime, size),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _cache[smodPath] = fresh;
        return (fresh.Value.Manifest, fresh.Value.Error);
    }

    /// <summary>Liest ein installiertes Mod-Verzeichnis. In der Regel enthält
    /// jedes <c>&lt;ModsDir&gt;/&lt;ModReference&gt;/</c>-Unterverzeichnis eine
    /// <c>data.json</c> mit dem Manifest. Wenn nicht vorhanden: null-Manifest.</summary>
    public (SmodManifest? Manifest, string? Error) ReadFromDirectory(string modDir)
    {
        try
        {
            var dataJsonPath = Path.Combine(modDir, "data.json");
            if (!File.Exists(dataJsonPath))
                return (null, "data.json nicht im Mod-Ordner gefunden");
            return (ParseDataJson(File.ReadAllText(dataJsonPath)), null);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "data.json-Read fehlgeschlagen: {Dir}", modDir);
            return (null, ex.Message);
        }
    }

    public void InvalidateCache(string smodPath) => _cache.TryRemove(smodPath, out _);

    private static CacheEntry ReadFromDiskInternal(string smodPath, long mtime, long size)
    {
        try
        {
            using var archive = ZipFile.OpenRead(smodPath);
            var dataEntry = archive.GetEntry("data.json");
            if (dataEntry is null)
                return new CacheEntry(mtime, size, null, "data.json nicht in der .smod-ZIP gefunden");

            using var stream = dataEntry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return new CacheEntry(mtime, size, ParseDataJson(json), null);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Konnte data.json nicht lesen: {Path}", smodPath);
            return new CacheEntry(mtime, size, null, ex.Message);
        }
    }

    private static SmodManifest? ParseDataJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new SmodManifest(
                ModReference: root.TryGetProperty("mod_reference", out var mr) ? (mr.GetString() ?? "") : "",
                Name:         root.TryGetProperty("name", out var n)             ? (n.GetString() ?? "") : "",
                Version:      root.TryGetProperty("version", out var v)          ? (v.GetString() ?? "") : "",
                Description:  root.TryGetProperty("description", out var d)      ? (d.GetString() ?? "") : "",
                SmlVersion:   root.TryGetProperty("sml_version", out var sml)    ? (sml.GetString() ?? "") : "",
                GameVersion:  root.TryGetProperty("game_version", out var gv)    ? (gv.GetString() ?? "") : "",
                Objects:      ParseObjectPaths(root));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "data.json-Parse fehlgeschlagen");
            return null;
        }
    }

    private static List<string> ParseObjectPaths(JsonElement root)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("objects", out var objs) || objs.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var obj in objs.EnumerateArray())
        {
            if (obj.TryGetProperty("path", out var p) && p.GetString() is { Length: > 0 } path)
                result.Add(path);
        }
        return result;
    }
}
