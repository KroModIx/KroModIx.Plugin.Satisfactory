using System.IO;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>
/// Löst den Satisfactory-Mods-Ordner auf. Satisfactory (Coffee Stain, Unreal
/// Engine 5) verwendet den SMM-Standard-Pfad:
///
/// <para><c>&lt;InstallDir&gt;/FactoryGame/Mods/</c></para>
///
/// <para>Jeder Mod ist ein Unterordner mit einer <c>&lt;ModReference&gt;/</c>-Struktur
/// die <c>data.json</c> (Manifest) und die eigentlichen Content-PAKs enthält.
/// SMM/ficsit-cli und wir schreiben in denselben Ordner — die Konvention ist
/// stabil und wird von Satisfactory Modding Loader (SML) gelesen.</para>
///
/// <para><b>Linux via Steam Proton:</b> der InstallDir zeigt auf den normalen
/// Steam-Common-Pfad (<c>~/.local/share/Steam/steamapps/common/Satisfactory</c>
/// oder auf einer Zusatzplatte). Keine Wine-Prefix-Traversal nötig — Satisfactory
/// speichert Mods im Install-Ordner, nicht im Compat-Prefix (im Gegensatz zu
/// z. B. LS25 wo Configs im XP-Style-Prefix liegen).</para>
///
/// <para><b>Native Linux</b> existiert für Satisfactory nicht (Windows-only,
/// aber via Proton spielbar). Der Path-Resolver behandelt beide Plattformen
/// identisch weil Steam den Windows-Install-Ordner-Style überall verwendet.</para>
/// </summary>
public sealed class SatisfactoryPathResolver
{
    /// <summary>Bekannte Schreibweisen des Mods-Ordners, in Prioritaet.
    /// Der erste Eintrag ist der kanonische — er wird angelegt, wenn keiner
    /// existiert.</summary>
    private static readonly string[] ModDirCandidates =
    {
        "FactoryGame/Mods",   // SMM/ficsit-cli-Standard
        "Mods",               // vereinzelt von Hand so angelegt
    };

    /// <summary>Der vorhandene Mods-Ordner, oder null wenn keiner existiert.
    /// Nur lesen — legt nichts an. Fuer Anzeige-Pfade.</summary>
    public string? GetModsDir(DetectedGame game)
    {
        if (string.IsNullOrEmpty(game.InstallDir)) return null;
        return ModFolderDiscovery.Find(game.InstallDir, ModDirCandidates);
    }

    /// <summary>Der Mods-Ordner, angelegt falls noetig (v0.12.0).
    ///
    /// <para>Vorher stand hier ein festes <c>Path.Combine(InstallDir,
    /// "FactoryGame", "Mods")</c>. Der Ordner gehoert aber nicht zur
    /// Vanilla-Installation — er entsteht erst durch SML oder die erste Mod
    /// und ist nach einer Neuinstallation weg. Das Plugin meldete dann
    /// „Mods-Ordner existiert nicht", zeigte eine leere Liste, und
    /// installieren ging auch nicht, weil das Ziel fehlte. Jetzt wird der
    /// kanonische Pfad angelegt — genau den legt SMM auch an — und eine
    /// abweichende Schreibweise (<c>mods/</c> unter Linux) wird gefunden
    /// statt uebersehen.</para></summary>
    public string? EnsureModsDir(DetectedGame game)
    {
        if (string.IsNullOrEmpty(game.InstallDir)) return null;
        return ModFolderDiscovery.FindOrCreate(game.InstallDir, ModDirCandidates);
    }
}
