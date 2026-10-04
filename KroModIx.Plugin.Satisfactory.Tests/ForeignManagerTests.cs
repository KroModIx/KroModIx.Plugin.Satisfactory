using System;
using System.IO;
using FluentAssertions;
using KroModIx.Plugin.Satisfactory.Services;
using Xunit;

namespace KroModIx.Plugin.Satisfactory.Tests;

/// <summary>Einträge, die ein anderer Mod-Manager ausgeliefert hat. Der
/// Anlass ist bezahlt, nur in einem anderen Plugin: am 04.10.2026 hat ein
/// Deinstallieren-Klick im Icarus-Plugin lmms zusammengeführtes Pak entfernt
/// und damit lautlos eine Mod aus dem Spiel genommen — die Quelle lag
/// unversehrt in lmms Zwischenspeicher, und gesucht wurde der Fehler danach
/// stundenlang im Spiel.</summary>
public sealed class ForeignManagerTests : IDisposable
{
    private readonly string _tmp = Directory.CreateTempSubdirectory("fremd-satisfactory").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    private string EigenerEintrag(string name)
    {
        var p = Path.Combine(_tmp, name);
        File.WriteAllText(p, "inhalt");
        return p;
    }

    /// <summary>So liefert lmm aus: ein Verweis in den eigenen
    /// Zwischenspeicher.</summary>
    private string FremderVerweis(string name)
    {
        var cache = Path.Combine(_tmp, "share", "lmm", "cache");
        Directory.CreateDirectory(cache);
        var ziel = Path.Combine(cache, name);
        File.WriteAllText(ziel, "inhalt");
        var link = Path.Combine(_tmp, name + "-link");
        File.CreateSymbolicLink(link, ziel);
        return link;
    }

    private static dynamic Erkenne(string pfad)
        => new InstalledSmodMod(pfad, "MeinMod", 1, DateTime.UtcNow, null, null).MitVerwalterErkennung();

    [Fact]
    public void Ein_eigener_Eintrag_bleibt_veraenderbar()
    {
        var mod = Erkenne(EigenerEintrag("MeinMod"));

        ((bool)mod.CanModify).Should().BeTrue();
        ((string?)mod.ManagedBy).Should().BeNull();
    }

    [Fact]
    public void Ein_fremd_ausgelieferter_Eintrag_wird_erkannt_und_benannt()
    {
        var mod = Erkenne(FremderVerweis("FremdMod"));

        ((bool)mod.CanModify).Should().BeFalse();
        ((string?)mod.ManagedBy).Should().Be("lmm");
    }
}
