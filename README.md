# KroModIx.Plugin.Satisfactory

[![CI](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/actions/workflows/ci.yml/badge.svg)](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/actions/workflows/ci.yml)

Satisfactory (Coffee Stain) Mod-Manager als Plugin für den [KroModIx](https://github.com/KroModIx/KroModIx).

## Ziel-Spiel

- **Satisfactory** (Steam App-ID 526870)
  - Mods-Ordner: `<Satisfactory-Install>/FactoryGame/Mods/<ModReference>/`
  - Auf Linux via Steam Proton — kein Wine-Prefix-Umweg, Mods bleiben im
    Install-Ordner.

## Features (v0.1.0)

- **Tab „Katalog"** — ficsit.app-Mods im 24-h-Cache. Rows mit Cover, Popularity,
  Downloads, View-Count, Update-Datum. Suche über Name/mod_reference/Beschreibung.
  **Doppelklick oder 🔍-Button öffnet Detail-Dialog** mit vollständiger
  Markdown-Beschreibung, Autoren, Kompatibilität (EA/EXP), Source-Link und
  KI-Zusammenfassung (via Host-KI-Provider). **⬇ Download-Button** lädt die
  neueste .smod direkt in den Downloads-Ordner (kein OAuth nötig).
- **Tab „Downloads"** — Heruntergeladene .smod-Files mit Cover-Enrichment via
  ficsit-API. Auto-Refresh via FileSystemWatcher. Install-Button entpackt
  nach `FactoryGame/Mods/<ModReference>/`, Delete-Button.
- **Tab „Installiert"** — Mod-Ordner im Satisfactory-Install mit
  data.json-Metadata + Enrichment (Autoren, Beschreibung, Cover). Detail-
  Dialog per Doppelklick, Ordner-öffnen, Deinstallieren.
- **Tab „Einstellungen"** — Katalog-Cache-Alter, Standard-Sortierung.
  Kein API-Key nötig (ficsit-API ist offen).
- **IUpdateNotifier**: grüner ↑-Badge auf der Satisfactory-Kachel bei neuen
  Katalog-Einträgen.

## API

Nutzt die öffentliche **ficsit.app GraphQL-API v2** (`https://api.ficsit.app/v2/query`).
Kein OAuth für Read-Queries, kein API-Key erforderlich. Direct-Download-URLs
funktionieren für alle User.

## Installation

Aus [Release](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/releases)
das ZIP entpacken nach:

- **Linux:** `~/.config/KroModIx/plugins/kroste.satisfactory/`
- **Windows:** `%APPDATA%\KroModIx\plugins\kroste.satisfactory\`

Alternativ: 1-Klick-Install über die Install-Karte in der KroModIx-Sidebar
(Host v0.3+).

## Lizenz

MIT.
