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

## Archive kommen aus dem Host, und es gibt jetzt Tests (ab v0.13.0)

`SmodInstallService` bekommt `IHostServices.Archives`.

**`ZipFile.ExtractToDirectory` hatte keine Lücke** — das ist wichtig
festzuhalten, damit die Änderung nicht falsch erinnert wird. Nachgemessen am
03.10.2026 mit einem Archiv, dessen zweiter Eintrag ein absoluter Pfad ist:

```
MESSUNG: IOException: Extracting Zip entry would have resulted in a file
         outside the specified destination directory.
MESSUNG: Datei ausserhalb vorhanden = False
MESSUNG: gut.txt im Ziel vorhanden  = True
```

Der Ausbruch wird abgewiesen. Das Problem ist die **dritte** Zeile: es
bricht mitten im Vorgang ab und lässt die bereits entpackten Dateien liegen.
Und weil der Update-Weg das Zielverzeichnis **vorher löscht**, stand der
Nutzer dann mit einer halb installierten Mod da, wo vorher eine ganze war —
mit einer Meldung aus dem Framework.

**Jetzt wird vor dem Löschen geprüft.** Ist ein Eintrag unsicher, passiert
gar nichts: die alte Fassung bleibt vollständig stehen, und die Meldung sagt
das. Geprüft wird auch der `ModReference` aus dem Manifest — er kommt aus
dem Archiv und bildet den Zielordnernamen.

**Was bewusst nicht migriert ist:** `SmodMetadataReader`. Der liest ein
Manifest **in den Speicher**, und dafür hat der Host-Baukasten keine
Schnittstelle — er packt aus, er liefert keine Bytes. Nachgemessen am
03.10.2026, ob sich eine lohnt: nur **zwei** Stellen über alle Plugins tun
das (hier und LS25' `ModDescReader`); die anderen drei
Archiv-Leser (Icarus' `PakBackupService`, LS25' `ModBackupService`,
Ren'PyAssists `RenpySaveService`) **erzeugen** auch Archive und können
ohnehin nicht wandern. Zwei Verbraucher, die nichts ins Spiel schreiben,
rechtfertigen keine neue Host-Schnittstelle.

**Das Repo hatte bis v0.13.0 kein Testprojekt**, obwohl der Install-Pfad ins
Spielverzeichnis schreibt. Jetzt `KroModIx.Plugin.Satisfactory.Tests`
(xunit.v3 + FluentAssertions + `KroModIx.Plugin.TestKit`) mit sieben Tests,
und `dotnet test` läuft in der CI.
