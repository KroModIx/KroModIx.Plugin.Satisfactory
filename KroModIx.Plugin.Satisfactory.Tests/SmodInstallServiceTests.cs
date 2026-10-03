using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using KroModIx.Plugin.Satisfactory.Services;
using KroModIx.Plugin.TestKit;
using Xunit;

namespace KroModIx.Plugin.Satisfactory.Tests;

/// <summary>Der Install-Pfad schreibt ins Spielverzeichnis und hatte bis
/// v0.9.0 <b>kein Testprojekt im Repo</b>.
///
/// <para><b>Was sich geändert hat.</b> Vorher lief das über
/// <c>ZipFile.ExtractToDirectory</c>. Das hat <b>keine Lücke</b> gelassen —
/// nachgemessen am 03.10.2026: ein Eintrag außerhalb des Ziels führt zu
/// <c>IOException: Extracting Zip entry would have resulted in a file
/// outside the specified destination directory.</c> und die Datei wird nicht
/// geschrieben. Es bricht aber <b>mitten im Vorgang</b> ab und lässt die
/// bereits entpackten Dateien liegen — und weil der Update-Weg das
/// Zielverzeichnis vorher löscht, stand der Nutzer dann mit einer halb
/// installierten Mod da, wo vorher eine ganze war. Jetzt werden die Einträge
/// <b>vor</b> dem Löschen geprüft.</para></summary>
public sealed class SmodInstallServiceTests : IDisposable
{
    private readonly string _tmp;
    private readonly string _modsDir;
    private readonly FakeArchiveService _archives = new();
    private readonly SmodInstallService _sut;

    public SmodInstallServiceTests()
    {
        _tmp = Directory.CreateTempSubdirectory("kromodix-sf-smod").FullName;
        _modsDir = Path.Combine(_tmp, "Mods");
        Directory.CreateDirectory(_modsDir);
        _sut = new SmodInstallService(_modsDir, Path.Combine(_tmp, "downloads"),
            new SmodMetadataReader(), _archives);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    /// <summary>Eine .smod ist eine ZIP mit <c>&lt;ModRef&gt;.uplugin</c> im
    /// Wurzelverzeichnis (SMM v3+).</summary>
    private string BuildSmod(string name, string modRef, params string[] weitereEintraege)
    {
        var path = Path.Combine(_tmp, name);
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var s = new StreamWriter(zip.CreateEntry($"{modRef}.uplugin").Open()))
            s.Write($"{{\"FriendlyName\":\"{modRef}\",\"SemVersion\":\"1.0.0\"}}");
        foreach (var e in weitereEintraege)
            using (var s = zip.CreateEntry(e).Open())
                s.Write([1, 2, 3]);
        return path;
    }

    [Fact]
    public void Install_legt_den_Mod_Ordner_nach_ModReference_an()
    {
        var smod = BuildSmod("MeinMod.smod", "MeinMod",
            "Binaries/Win64/MeinMod.dll", "Resources/icon.png");

        var mod = _sut.Install(smod);

        mod.ModReference.Should().Be("MeinMod");
        File.Exists(Path.Combine(_modsDir, "MeinMod", "MeinMod.uplugin")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "MeinMod", "Binaries", "Win64", "MeinMod.dll"))
            .Should().BeTrue();
        mod.DirSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Zweiter_Install_ohne_Ueberschreiben_schlaegt_fehl()
    {
        var smod = BuildSmod("MeinMod.smod", "MeinMod", "Binaries/a.dll");
        _sut.Install(smod);

        ((Action)(() => _sut.Install(smod))).Should().Throw<IOException>()
            .WithMessage("*existiert bereits*");
    }

    [Fact]
    public void Mit_Ueberschreiben_wird_der_Ordner_ersetzt()
    {
        var alt = BuildSmod("MeinMod.smod", "MeinMod", "Binaries/alt.dll");
        _sut.Install(alt);
        var neu = BuildSmod("MeinMod2.smod", "MeinMod", "Binaries/neu.dll");

        _sut.Install(neu, overwrite: true);

        File.Exists(Path.Combine(_modsDir, "MeinMod", "Binaries", "neu.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "MeinMod", "Binaries", "alt.dll"))
            .Should().BeFalse("der Ordner wird ersetzt, nicht ergänzt");
    }

    /// <summary>Der Kern der Änderung: der Ausbruchsversuch wird <b>vor</b>
    /// dem Löschen erkannt, die vorhandene Fassung bleibt vollständig.</summary>
    [Fact]
    public void Ausbruchsversuch_laesst_die_vorhandene_Fassung_unangetastet()
    {
        var gut = BuildSmod("MeinMod.smod", "MeinMod", "Binaries/gut.dll");
        _sut.Install(gut);

        var opfer = Path.Combine(_tmp, "ausserhalb.dll");
        var boese = BuildSmod("MeinMod-boese.smod", "MeinMod", "Binaries/neu.dll", opfer);

        ((Action)(() => _sut.Install(boese, overwrite: true))).Should()
            .Throw<InvalidDataException>().WithMessage("*herausschreiben*");

        File.Exists(opfer).Should().BeFalse();
        File.Exists(Path.Combine(_modsDir, "MeinMod", "Binaries", "gut.dll"))
            .Should().BeTrue("die alte Fassung darf nicht angefasst werden");
        File.Exists(Path.Combine(_modsDir, "MeinMod", "Binaries", "neu.dll"))
            .Should().BeFalse("und nichts aus dem bösen Archiv darf landen");
    }

    [Fact]
    public void Punkt_Punkt_Pfad_wird_abgelehnt()
    {
        var boese = BuildSmod("boese.smod", "MeinMod", "../../../evil.dll");

        ((Action)(() => _sut.Install(boese))).Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(_tmp, "evil.dll")).Should().BeFalse();
    }

    /// <summary>Der <c>ModReference</c> kommt aus dem Manifest <b>im
    /// Archiv</b> und ist damit genauso unvertrauenswürdig wie ein
    /// Eintragspfad — er bildet den Zielordnernamen.</summary>
    [Fact]
    public void Unsicherer_ModReference_wird_abgelehnt()
    {
        var path = Path.Combine(_tmp, "boese2.smod");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var s = new StreamWriter(zip.CreateEntry("data.json").Open()))
            s.Write("""{"mod_reference":"../../ausbruch","version":"1.0.0"}""");

        ((Action)(() => _sut.Install(path))).Should().Throw<InvalidDataException>();
        Directory.Exists(Path.Combine(_tmp, "ausbruch")).Should().BeFalse();
    }

    [Fact]
    public void Ohne_Manifest_schlaegt_Install_fehl()
    {
        var path = Path.Combine(_tmp, "ohne-manifest.smod");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var s = zip.CreateEntry("Binaries/a.dll").Open())
            s.Write([1]);

        ((Action)(() => _sut.Install(path))).Should().Throw<InvalidDataException>()
            .WithMessage("*Kein gültiges Manifest*");
    }
}
