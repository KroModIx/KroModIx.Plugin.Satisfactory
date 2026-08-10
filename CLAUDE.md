# KroModIx.Plugin.Satisfactory

## Grundlagen

- **Was:** Satisfactory-Mod-Manager als Plugin für KroModIx. Zielspiel:
  Satisfactory (Steam App-ID 526870, Coffee Stain).
- **Stack:** .NET 10, `KroModIx.Plugin.Contracts` v1.7.0 als PackageReference.
- **Repo:** `github.com/KroModIx/KroModIx.Plugin.Satisfactory`.
- **Deploy-Ziel:** `~/.config/KroModIx/plugins/kroste.satisfactory/` bzw.
  `%APPDATA%\KroModIx\plugins\kroste.satisfactory\`.
- **API:** [ficsit.app](https://ficsit.app) GraphQL v2
  (`https://api.ficsit.app/v2/query`), kein OAuth für Read-Queries.

## Architektur

- **Services/Ficsit/**: `FicsitApiClient` (GraphQL via `HttpClient` +
  `System.Text.Json`), `FicsitCatalogService` (Pagination, 24 h Disk-Cache,
  Age-Check), `FicsitSettingsService` (Cache-Hours, DefaultSort — kein Secret),
  `FicsitUpdateTracker` (Baseline-basiert für IUpdateNotifier),
  `FicsitCatalogEntry`/`FicsitModDetail` (Records).
- **Services/**: `SatisfactoryPathResolver` (FactoryGame/Mods),
  `SatisfactoryPaths` (Downloads/Cache/Cover-Dirs), `SmodMetadataReader`
  (data.json aus .smod-ZIP, mit `ConcurrentDictionary<Path, Lazy<CacheEntry>>`-
  Cache analog LS25 v1.8.2), `SmodInstallService` (ListInstalled aus
  Mod-Ordnern, ListDownloaded aus DownloadsDir, Install = ZIP-Extract in
  `<ModsDir>/<mod_reference>/`, Uninstall = `Directory.Delete recursive`,
  Download-Streaming mit Progress), `HtmlStrip` (Markdown/HTML/BBCode-Strip
  wie Icarus).
- **Views/**: CatalogView + Downloads + InstalledSmods + Settings,
  ModDetailWindow. Alle View-Model-Refreshes off-thread (perf.md Regel 0)
  auch wenn PathResolver-Enumerate billig ist — Konsistenz zum LS25/Icarus-
  Muster. Cover-Load off-UI-Thread. Nexus-Enrichment sequenziell mit 250 ms
  Throttle.

## v0.1.0 — was drin ist

- Katalog-Tab: Rows mit Cover, Popularity/Downloads/Views/Updated, Suche,
  Detail-Dialog per Doppelklick, Direct-Download-Button.
- Detail-Dialog: Cover, Autor(en), Version, Kompatibilität (EA/EXP),
  Source-URL, ficsit-Link, Beschreibung (HTML/Markdown-stripped),
  KI-Zusammenfassung via `_host.Ai`, Download-Button.
- Downloads-Tab: heruntergeladene .smod-Files mit Cover/Enrichment,
  Install-Button (entpackt nach FactoryGame/Mods/), Details-Button, Delete.
- Installiert-Tab: Ordner in FactoryGame/Mods/ mit data.json-Metadata +
  Enrichment. Details-Dialog, Ordner-öffnen, Deinstallieren.
- Settings-Tab: Cache-Refresh-Hours + DefaultSort (Popularity/Hotness/…).
- IUpdateNotifier: grüner ↑-Badge bei neuen Katalog-Einträgen.

## v0.2 — Roadmap

- Enable/Disable pro Mod (SMM-Style Profile via separates profiles.json —
  nicht Dateisystem-Rename wie LS25).
- Backup/Restore der installierten Mod-Ordner als ZIP.
- Dependency-Auflösung: `getMod.versions.dependencies` prüfen + parallel-
  Download.
- Update-Check pro installiertem Mod (Version-Vergleich mit ficsit-Katalog).
- Compatibility-Warnung im Detail-Dialog wenn EA/EXP „Damaged" oder „Broken".

## Referenz

- **SMM (Satisfactory Mod Manager):** GitHub
  [`satisfactorymodding/SatisfactoryModManager`](https://github.com/satisfactorymodding/SatisfactoryModManager)
  (Svelte + Go) und
  [`satisfactorymodding/ficsit-cli`](https://github.com/satisfactorymodding/ficsit-cli)
  (Go). Referenz für alle GraphQL-Queries — die Query-Definitionen in
  `ficsit-cli/ficsit/queries/*.graphql` waren die Grundlage für
  `FicsitApiClient`.
- **Kroste-Plugin-Skill:** `~/.claude/skills/KroModIx-Plugin/` — Struktur-
  Konventionen, perf.md Regel 0 (Refresh off-thread), Regel 0b (File-Reader-
  Cache).
- **Vorbild-Plugins:** LS25 (v1.8.2 — Metadata-Cache-Pattern) und Icarus
  (v1.10.0 — Detail-Dialog + Card-Layout).

## Bekannte Grenzen

- **v0.1.0 kennt kein Enable/Disable pro Mod** — alles was in
  `FactoryGame/Mods/` liegt, wird von SML beim Spiel-Start geladen. Wer's
  temporär abschalten will: Ordner wegkopieren oder mit „Deinstallieren" weg.
  SMM-kompatible Profile kommen in v0.2.
- **Kein Dependency-Resolver** — Mods die auf andere Mods aufbauen erfordern
  manuellen Zusatz-Install. v0.2.
- **Compatibility-Status wird nur angezeigt**, nicht als Blocker verwendet.
