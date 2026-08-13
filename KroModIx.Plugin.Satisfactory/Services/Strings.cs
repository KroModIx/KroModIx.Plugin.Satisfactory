using System.Collections.Generic;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.Satisfactory.Services;

/// <summary>Uebersetzungs-Tabelle fuer alle User-facing Strings im
/// Satisfactory-Plugin. Sprachen: <c>de</c> (Fallback) + <c>en</c>.
///
/// <para>Nutzung: <c>Strings.Init(host.Localization)</c> beim Plugin-Init,
/// dann ueberall <c>Strings.T("key")</c>. Bei fehlendem Key wird der Key
/// selbst zurueckgegeben (macht Missing-Translations sofort sichtbar).</para>
///
/// <para><b>Kein Live-Refresh bei Sprachwechsel:</b> die Strings werden
/// zum View-Constructor-Zeitpunkt gelesen. Bei Sprachwechsel im Host muss
/// der User die Satisfactory-Kachel neu waehlen (Host-Tab-Cache erzeugt dann
/// neue View-Instanzen mit den frischen Uebersetzungen) oder die App neu
/// starten. Vollreactive-Bindings waeren komplex und lohnen sich fuer den
/// seltenen Anwendungsfall nicht.</para></summary>
public static class Strings
{
    private static ILocalization? _loc;

    public static void Init(ILocalization loc) => _loc = loc;

    public static string T(string key)
    {
        var iso = _loc?.CurrentIso ?? "de";
        if (iso.StartsWith("en") && En.TryGetValue(key, out var en)) return en;
        if (De.TryGetValue(key, out var de)) return de;
        return key;
    }

    private static readonly Dictionary<string, string> De = new()
    {
        // KI-Prompts (v0.7 sprachabhaengig)
        ["ai.prompt.summary_system"] = "Du bist ein deutschsprachiger Satisfactory-Mod-Reviewer. Fasse die Mod-Beschreibung in 3-5 Saetzen zusammen: Was macht der Mod? Welche Features/Maschinen/Balance-Aenderungen? Fuer welchen Spielstil (QoL, Cheat, harder Late-Game, Cosmetic)? Sachlich, kein Werbe-Sprech. Antworte auf Deutsch.",

        // Tab-Labels
        ["tab.installed"] = "Installiert",
        ["tab.catalog"] = "Katalog",
        ["tab.downloads"] = "Downloads",
        ["tab.settings"] = "Einstellungen",

        // Common buttons
        ["btn.refresh"] = "↺  Aktualisieren",
        ["btn.check_updates"] = "🔄  Updates prüfen",
        ["btn.update_all"] = "⬆  Alle updaten",
        ["btn.mods_folder"] = "📂  Mods-Ordner",
        ["btn.downloads_folder"] = "📂  Downloads-Ordner",
        ["btn.open_downloads_folder"] = "📂  Downloads-Ordner öffnen",
        ["btn.install_all"] = "📥  Alle installieren",
        ["btn.install"] = "📥  Installieren",
        ["btn.details"] = "🔍  Details",
        ["btn.open_dir"] = "📂  Ordner",
        ["btn.uninstall"] = "🗑  Deinstallieren",
        ["btn.delete"] = "🗑  Löschen",
        ["btn.download"] = "⬇  Download",
        ["btn.download_long"] = "⬇  Herunterladen",
        ["btn.update"] = "⬆  Update",
        ["btn.open_ficsit"] = "↗  ficsit öffnen",
        ["btn.open_ficsit_long"] = "↗  Auf ficsit.app öffnen",
        ["btn.open_source"] = "↗  Source",
        ["btn.ai_summary"] = "🤖  KI-Zusammenfassung",
        ["btn.close"] = "Schließen",
        ["btn.save"] = "💾  Speichern",
        ["btn.open_ficsit_app"] = "↗  ficsit.app",
        ["btn.open_docs"] = "📖  SMM-Docs",

        // Placeholders + tooltips
        ["placeholder.search_installed"] = "Installierte Mods filtern …",
        ["placeholder.search_catalog"] = "Katalog filtern (Name/ModReference/Beschreibung) …",
        ["tooltip.check_updates"] = "Prüft für jeden installierten Mod ob ficsit eine neuere Version anbietet (throttled 250 ms pro Mod).",
        ["tooltip.update_all"] = "Installiert alle Updates sequenziell (Rate-Limit-Ruecksicht). Erst 'Updates pruefen' klicken damit was zu tun ist.",
        ["tooltip.install_all"] = "Installiert alle .smod-Downloads (überschreibt bestehende Versionen). Ideal nach einem Update-Batch.",
        ["tooltip.download_catalog"] = "Direct-Download der neuesten .smod in den Downloads-Ordner",
        ["tooltip.download_detail"] = "Direct-Download der neuesten .smod-Version in den Downloads-Ordner",

        // Status messages
        ["status.loading_catalog"] = "Katalog wird geladen …",
        ["status.reading_installed"] = "Installierte Mods werden gelesen …",
        ["status.reading_downloads"] = "Downloads werden gelesen …",
        ["status.no_mods"] = "Keine Mods in FactoryGame/Mods.",
        ["status.no_downloads"] = "Keine .smod-Dateien im Downloads-Ordner.",
        ["status.mods_count_size"] = "{0} Mods · {1:F1} MB",
        ["status.downloads_count_size"] = "{0} .smod · {1:F1} MB gesamt",
        ["status.read_error_prefix"] = "Fehler beim Lesen: ",
        ["status.catalog_count"] = "{0} Mods (Cache-Alter: {1} h)",
        ["status.catalog_load_error"] = "Fehler beim Laden: ",
        ["status.detail_loading"] = "Detail wird geladen …",
        ["status.detail_load_error"] = "Fehler beim Laden.",
        ["status.detail_load_failed"] = "Detail konnte nicht geladen werden (API-Fehler).",
        ["status.detail_desc_placeholder"] = "Detail-Beschreibung wird geladen …",
        ["status.detail_no_desc"] = "Keine Beschreibung im Detail-Endpoint.",
        ["status.no_version"] = "Keine Version verfügbar.",
        ["status.updates_found"] = "Updates gefunden: {0} Mod(s).",
        ["status.no_updates"] = "Keine Updates.",
        ["status.error_prefix"] = "Fehler: ",
        ["status.mods_dir_prefix"] = "Mods: {0}",
        ["status.downloads_dir_prefix"] = "Ordner: {0}",

        // Row labels
        ["row.status_prefix"] = "⬆ Update v{0}",

        // Notifications
        ["notify.uninstalled_prefix"] = "Deinstalliert: ",
        ["notify.installed_prefix"] = "Installiert: ",
        ["notify.downloaded_prefix"] = "Heruntergeladen: ",
        ["notify.deleted_prefix"] = "Gelöscht: ",
        ["notify.no_mod_reference"] = "Kein mod_reference verfügbar — kann Detail nicht öffnen.",
        ["notify.no_mod_reference_file"] = "Kein mod_reference im .smod-Manifest: {0}",
        ["notify.no_downloads"] = "Keine Downloads zu installieren.",
        ["notify.bulk_install_ok"] = "{0} .smods installiert.",
        ["notify.bulk_install_partial"] = "{0} installiert, {1} Fehler (siehe Log).",
        ["notify.no_download_version"] = "Keine Download-Version für {0}.",
        ["notify.no_download_ficsit"] = "Keine Download-Version für {0} bei ficsit.",
        ["notify.download_error_prefix"] = "Download-Fehler: ",
        ["notify.no_download_link"] = "Kein Download-Link verfügbar — Detail noch am Laden?",
        ["notify.detail_wait"] = "Bitte warten bis Detail geladen ist.",
        ["notify.ai_unavailable"] = "KI-Provider nicht erreichbar — bitte in den KroModIx-Einstellungen konfigurieren.",
        ["notify.no_mod_reference_update"] = "Kein ModReference — kann Update nicht auflösen.",
        ["notify.update_install_ok"] = "Update installiert: {0} → v{1}",
        ["notify.update_error_prefix"] = "Update-Fehler: ",
        ["notify.no_updates_hint"] = "Keine offenen Updates. Erst 🔄 Updates prüfen klicken.",
        ["notify.bulk_update_ok"] = "{0} Mod(s) aktualisiert.",
        ["notify.bulk_update_partial"] = "{0} aktualisiert, {1} Fehler.",
        ["notify.settings_saved"] = "ficsit-Einstellungen gespeichert.",
        ["notify.updates_hint_summary_fallback"] = "{0} Mod-Update(s) verfügbar",

        // Dialogs
        ["dialog.uninstall_title"] = "Mod deinstallieren",
        ["dialog.uninstall_msg"] = "„{0}\" wirklich löschen? Der Ordner {1} wird komplett entfernt.",
        ["dialog.delete_download_title"] = "Download löschen",
        ["dialog.delete_download_msg"] = "„{0}\" aus dem Downloads-Ordner löschen?",
        ["dialog.ok_delete"] = "Löschen",
        ["dialog.cancel"] = "Abbrechen",

        // Detail-Dialog labels
        ["detail.window_title"] = "ficsit-Mod-Detail",
        ["detail.section.description"] = "Beschreibung",
        ["detail.section.ai_summary"] = "🤖 KI-Zusammenfassung",
        ["detail.meta.authors"] = "Autor(en)",
        ["detail.meta.version"] = "Version",
        ["detail.meta.updated"] = "Aktualisiert",
        ["detail.meta.downloads"] = "Downloads",
        ["detail.meta.compatibility"] = "Kompatibilität",
        ["detail.busy_download"] = "…Download läuft…",
        ["detail.busy_ai"] = "…KI läuft…",
        ["detail.status_prefix"] = "{0} · {1}",
        ["detail.ai_running_prefix"] = "KI-Zusammenfassung via {0} …",
        ["detail.ai_no_answer"] = "KI hat keine Antwort geliefert.",

        // Progress messages
        ["progress.updates_count"] = "{0} Updates …",
        ["progress.update_of"] = "Update {0}/{1}: {2}",
        ["progress.update_title_prefix"] = "Update: {0}",
        ["progress.detail_load"] = "Detail laden …",
        ["progress.download_prefix"] = "Download v{0} …",
        ["progress.install"] = "Install …",
        ["progress.download_row"] = "{0} v{1} · {2}%",
        ["progress.download_row_simple"] = "{0} · {1}%",
        ["progress.ficsit_prefix"] = "ficsit: {0}",
        ["progress.download_start"] = "Download startet …",
        ["progress.download_prefix_simple"] = "Download {0} …",
        ["progress.download_file"] = "{0} · {1}%",
        ["progress.install_downloads"] = "Installiere {0} .smod-Downloads …",
        ["progress.install_row"] = "Installiere {0}/{1}: {2}",

        // Settings
        ["settings.title"] = "ficsit-Einstellungen",
        ["settings.info"] = "Die ficsit.app-API ist offen — kein API-Key nötig. Diese Einstellungen steuern nur den Katalog-Cache im Plugin.",
        ["settings.cache_age_label"] = "Katalog-Cache-Alter (Stunden):",
        ["settings.sort_label"] = "Standard-Sortierung:",
        ["settings.links_label"] = "Nützliche Links:",
        ["settings.saved"] = "Einstellungen gespeichert.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // AI prompts (v0.7 language-aware)
        ["ai.prompt.summary_system"] = "You are an English-language Satisfactory mod reviewer. Summarize the mod description in 3-5 sentences: What does the mod do? Which features/machines/balance changes? For which playstyle (QoL, cheat, harder late-game, cosmetic)? Factual, no marketing language. Respond in English.",

        ["tab.installed"] = "Installed",
        ["tab.catalog"] = "Catalog",
        ["tab.downloads"] = "Downloads",
        ["tab.settings"] = "Settings",

        ["btn.refresh"] = "↺  Refresh",
        ["btn.check_updates"] = "🔄  Check updates",
        ["btn.update_all"] = "⬆  Update all",
        ["btn.mods_folder"] = "📂  Mods folder",
        ["btn.downloads_folder"] = "📂  Downloads folder",
        ["btn.open_downloads_folder"] = "📂  Open downloads folder",
        ["btn.install_all"] = "📥  Install all",
        ["btn.install"] = "📥  Install",
        ["btn.details"] = "🔍  Details",
        ["btn.open_dir"] = "📂  Folder",
        ["btn.uninstall"] = "🗑  Uninstall",
        ["btn.delete"] = "🗑  Delete",
        ["btn.download"] = "⬇  Download",
        ["btn.download_long"] = "⬇  Download",
        ["btn.update"] = "⬆  Update",
        ["btn.open_ficsit"] = "↗  Open on ficsit",
        ["btn.open_ficsit_long"] = "↗  Open on ficsit.app",
        ["btn.open_source"] = "↗  Source",
        ["btn.ai_summary"] = "🤖  AI summary",
        ["btn.close"] = "Close",
        ["btn.save"] = "💾  Save",
        ["btn.open_ficsit_app"] = "↗  ficsit.app",
        ["btn.open_docs"] = "📖  SMM docs",

        ["placeholder.search_installed"] = "Filter installed mods …",
        ["placeholder.search_catalog"] = "Filter catalog (name / mod-reference / description) …",
        ["tooltip.check_updates"] = "Checks each installed mod against ficsit for a newer version (throttled 250 ms per mod).",
        ["tooltip.update_all"] = "Installs all updates sequentially (rate-limit friendly). Click 'Check updates' first so something is queued.",
        ["tooltip.install_all"] = "Installs all .smod downloads (overwrites existing versions). Ideal after an update batch.",
        ["tooltip.download_catalog"] = "Direct download of the latest .smod into the downloads folder",
        ["tooltip.download_detail"] = "Direct download of the latest .smod version into the downloads folder",

        ["status.loading_catalog"] = "Loading catalog …",
        ["status.reading_installed"] = "Reading installed mods …",
        ["status.reading_downloads"] = "Reading downloads …",
        ["status.no_mods"] = "No mods in FactoryGame/Mods.",
        ["status.no_downloads"] = "No .smod files in the downloads folder.",
        ["status.mods_count_size"] = "{0} mods · {1:F1} MB",
        ["status.downloads_count_size"] = "{0} .smod · {1:F1} MB total",
        ["status.read_error_prefix"] = "Read error: ",
        ["status.catalog_count"] = "{0} mods (cache age: {1} h)",
        ["status.catalog_load_error"] = "Load error: ",
        ["status.detail_loading"] = "Loading details …",
        ["status.detail_load_error"] = "Load error.",
        ["status.detail_load_failed"] = "Details could not be loaded (API error).",
        ["status.detail_desc_placeholder"] = "Loading description …",
        ["status.detail_no_desc"] = "No description in detail endpoint.",
        ["status.no_version"] = "No version available.",
        ["status.updates_found"] = "Updates found: {0} mod(s).",
        ["status.no_updates"] = "No updates.",
        ["status.error_prefix"] = "Error: ",
        ["status.mods_dir_prefix"] = "Mods: {0}",
        ["status.downloads_dir_prefix"] = "Folder: {0}",

        ["row.status_prefix"] = "⬆ Update v{0}",

        ["notify.uninstalled_prefix"] = "Uninstalled: ",
        ["notify.installed_prefix"] = "Installed: ",
        ["notify.downloaded_prefix"] = "Downloaded: ",
        ["notify.deleted_prefix"] = "Deleted: ",
        ["notify.no_mod_reference"] = "No mod_reference available — cannot open details.",
        ["notify.no_mod_reference_file"] = "No mod_reference in .smod manifest: {0}",
        ["notify.no_downloads"] = "No downloads to install.",
        ["notify.bulk_install_ok"] = "{0} .smods installed.",
        ["notify.bulk_install_partial"] = "{0} installed, {1} error(s) (see log).",
        ["notify.no_download_version"] = "No download version for {0}.",
        ["notify.no_download_ficsit"] = "No download version for {0} on ficsit.",
        ["notify.download_error_prefix"] = "Download error: ",
        ["notify.no_download_link"] = "No download link available — detail still loading?",
        ["notify.detail_wait"] = "Please wait until details are loaded.",
        ["notify.ai_unavailable"] = "AI provider not reachable — configure it in KroModIx settings.",
        ["notify.no_mod_reference_update"] = "No ModReference — cannot resolve update.",
        ["notify.update_install_ok"] = "Update installed: {0} → v{1}",
        ["notify.update_error_prefix"] = "Update error: ",
        ["notify.no_updates_hint"] = "No pending updates. Click 🔄 Check updates first.",
        ["notify.bulk_update_ok"] = "{0} mod(s) updated.",
        ["notify.bulk_update_partial"] = "{0} updated, {1} error(s).",
        ["notify.settings_saved"] = "ficsit settings saved.",
        ["notify.updates_hint_summary_fallback"] = "{0} mod update(s) available",

        ["dialog.uninstall_title"] = "Uninstall mod",
        ["dialog.uninstall_msg"] = "Really delete \"{0}\"? The folder {1} will be removed completely.",
        ["dialog.delete_download_title"] = "Delete download",
        ["dialog.delete_download_msg"] = "Delete \"{0}\" from the downloads folder?",
        ["dialog.ok_delete"] = "Delete",
        ["dialog.cancel"] = "Cancel",

        ["detail.window_title"] = "ficsit mod details",
        ["detail.section.description"] = "Description",
        ["detail.section.ai_summary"] = "🤖 AI summary",
        ["detail.meta.authors"] = "Author(s)",
        ["detail.meta.version"] = "Version",
        ["detail.meta.updated"] = "Updated",
        ["detail.meta.downloads"] = "Downloads",
        ["detail.meta.compatibility"] = "Compatibility",
        ["detail.busy_download"] = "…download running…",
        ["detail.busy_ai"] = "…AI running…",
        ["detail.status_prefix"] = "{0} · {1}",
        ["detail.ai_running_prefix"] = "AI summary via {0} …",
        ["detail.ai_no_answer"] = "AI returned no answer.",

        ["progress.updates_count"] = "{0} updates …",
        ["progress.update_of"] = "Update {0}/{1}: {2}",
        ["progress.update_title_prefix"] = "Update: {0}",
        ["progress.detail_load"] = "Loading detail …",
        ["progress.download_prefix"] = "Download v{0} …",
        ["progress.install"] = "Install …",
        ["progress.download_row"] = "{0} v{1} · {2}%",
        ["progress.download_row_simple"] = "{0} · {1}%",
        ["progress.ficsit_prefix"] = "ficsit: {0}",
        ["progress.download_start"] = "Download starting …",
        ["progress.download_prefix_simple"] = "Download {0} …",
        ["progress.download_file"] = "{0} · {1}%",
        ["progress.install_downloads"] = "Installing {0} .smod downloads …",
        ["progress.install_row"] = "Installing {0}/{1}: {2}",

        ["settings.title"] = "ficsit settings",
        ["settings.info"] = "The ficsit.app API is open — no API key required. These settings only control the plugin's catalog cache.",
        ["settings.cache_age_label"] = "Catalog cache age (hours):",
        ["settings.sort_label"] = "Default sort:",
        ["settings.links_label"] = "Useful links:",
        ["settings.saved"] = "Settings saved.",
    };
}
