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

## Stand

Die maßgebliche Feature-Liste steht in der `description` in `plugin.json` —
sie wird bei jedem Release mitgepflegt und ist damit die einzige Stelle, die
nicht veralten kann. Ergänzend die GitHub-Releases des Repos.

Hier bewusst keine Versions-Momentaufnahme: die vorherige Fassung dieser Datei
beschrieb noch v0.1.0, während das Repo längst deutlich weiter war.

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

- **Kein Enable/Disable pro Mod** — alles was in `FactoryGame/Mods/` liegt,
  wird von SML beim Spiel-Start geladen. Wer's temporär abschalten will:
  Ordner wegkopieren oder mit „Deinstallieren" weg. SMM macht das über ein
  separates `profiles.json`; portiert ist das bis heute nicht, die
  entsprechenden Hinweise stehen als Kommentar in `InstalledSmodMod.cs` und
  `SettingsView.cs`.
- **Kein Dependency-Resolver** — Mods die auf andere Mods aufbauen erfordern
  manuellen Zusatz-Install.
- **Compatibility-Status wird nur angezeigt**, nicht als Blocker verwendet.
