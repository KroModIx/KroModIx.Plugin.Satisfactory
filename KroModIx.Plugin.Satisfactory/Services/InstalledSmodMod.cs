using System;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Ein installierter Satisfactory-Mod. .smod-Format: eine ZIP-Datei
/// die eine <c>data.json</c> (Manifest) + die Content-Files (typischerweise
/// eine oder mehrere <c>.pak</c>) enthält. SMM/wir entpacken die .smod nach
/// <c>&lt;InstallDir&gt;/FactoryGame/Mods/&lt;ModReference&gt;/</c> — pro Mod ein
/// eigener Ordner mit dem <c>mod_reference</c>-Namen (stabile Kennung).
///
/// <para>v0.1.0-Vereinfachung: Kein Enable/Disable-Toggle (das läuft in SMM
/// über ein separates <c>profiles.json</c> das der SML beim Start liest — bei
/// uns kommt in v0.2). Alles was im Mods-Ordner liegt gilt als aktiv.</para></summary>
public sealed record InstalledSmodMod(
    string ModDir,
    string ModReference,
    long DirSizeBytes,
    DateTime InstalledUtc,
    SmodManifest? Manifest,
    string? ReadError)
{
    /// <summary>v0.14.0: gesetzt, wenn dieser Eintrag einem <b>anderen
    /// Mod-Manager</b> gehört (lmm, r2modman, Vortex, SMM/ficsit). Erkannt am
    /// Verweis, nicht am Namen — siehe
    /// <see cref="KroModIx.Plugin.Contracts.ForeignManagerDetection"/>.
    /// Solche Einträge werden gelistet, damit der Nutzer sieht was im Spiel
    /// liegt, aber nicht verändert: am 04.10.2026 hat ein
    /// Deinstallieren-Klick im Icarus-Plugin lmms Pak entfernt und damit
    /// lautlos eine Mod aus dem Spiel genommen, während ihre Quelle woanders
    /// unversehrt lag.</summary>
    public string? ManagedBy { get; init; }

    /// <summary>Ob das Plugin diesen Eintrag verändern darf.</summary>
    public bool CanModify => ManagedBy is null;

    /// <summary>Einmalige Erkennung beim Scan — der Scanner legt sie über
    /// seine Ergebnisliste. Als Methode und nicht als berechnete Eigenschaft,
    /// weil sonst jede Bindung in der Oberfläche einen Dateisystem-Zugriff
    /// auslöst.</summary>
    public InstalledSmodMod MitVerwalterErkennung()
        => KroModIx.Plugin.Contracts.ForeignManagerDetection.IsForeignManaged(ModDir, out var wer)
            ? this with { ManagedBy = wer }
            : this;
}


/// <summary>Aus <c>data.json</c> gelesen. Nicht alle Felder werden von jedem
/// Mod-Autor gefüllt — Nullable-Handling in <see cref="SmodMetadataReader"/>.</summary>
public sealed record SmodManifest(
    string ModReference,
    string Name,
    string Version,
    string Description,
    string SmlVersion,
    string GameVersion,
    System.Collections.Generic.List<string> Objects);

/// <summary>Ein heruntergeladenes .smod-File im plugin-eigenen Downloads-
/// Ordner. Nach Install wird die .smod NICHT gelöscht — der User kann später
/// nochmal installieren wenn er's aus dem Mods-Ordner geworfen hat.</summary>
public sealed record DownloadedSmod(
    string FilePath,
    string FileName,
    long FileSizeBytes,
    DateTime DownloadedUtc,
    SmodManifest? Manifest,
    string? ReadError);
