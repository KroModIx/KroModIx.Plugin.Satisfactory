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
    public string? GetModsDir(DetectedGame game)
    {
        if (string.IsNullOrEmpty(game.InstallDir) || !Directory.Exists(game.InstallDir))
            return null;
        return Path.Combine(game.InstallDir, "FactoryGame", "Mods");
    }
}
