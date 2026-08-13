# KroModIx.Plugin.Satisfactory

[![CI](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/actions/workflows/ci.yml/badge.svg)](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/KroModIx/KroModIx.Plugin.Satisfactory)](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/releases)

**Satisfactory** (Coffee Stain) Mod-Manager als Plugin für den
[KroModIx](https://github.com/KroModIx/KroModIx). Nutzt die offene
[ficsit.app](https://ficsit.app)-GraphQL-API — kein Login, kein API-Key,
Direct-Download für alle User.

## Ziel-Spiel

**Satisfactory** — Steam AppId 526870.

- Mods-Ordner: `<Satisfactory-Install>/FactoryGame/Mods/<ModReference>/`
- Auf Linux via Steam-Proton — kein Wine-Prefix-Umweg, Mods bleiben im
  Steam-Install-Ordner.

## Neu in v0.6.0
- **DE+EN-Übersetzung** aller User-facing Strings (106 Keys) — Tab-Labels,
  Buttons, Placeholders, Tooltips, Statusmeldungen, Notifications, Dialoge,
  Detail-Dialog + Settings-Tab. Sprachwechsel im Host schaltet nach Kachel-
  Reselect live um.

## Features (v0.5.0)

### Katalog-Tab
- ficsit.app-Mods im 24-h-Cache (~1500 Einträge)
- Rows mit Cover (WebP → PNG via ImageSharp), Popularity, Downloads, Views,
  Update-Datum
- Suche über Name / mod_reference / Beschreibung
- Doppelklick öffnet Detail-Dialog mit Markdown-Beschreibung, Autoren,
  Kompatibilität (EA/EXP), Source-Link, **KI-Zusammenfassung**
- **⬇ Download** lädt die neueste `.smod` direkt in den Downloads-Ordner

### Downloads-Tab
- Alle heruntergeladenen `.smod`-Files mit Cover + Autoren + Version +
  Beschreibung (via ficsit-Enrichment)
- **📥 Alle installieren** — Bulk-Install-Button
- Pro Row: Installieren + 🔍 Details + Löschen
- Auto-Refresh via FileSystemWatcher

### Installiert-Tab
- Mod-Ordner in `FactoryGame/Mods/` mit `.uplugin`-Manifest-Read (SMM v3-
  Konvention) und Fallback auf `data.json` (Legacy)
- Cover + Autoren + Beschreibung via ficsit-Enrichment
- **🔄 Updates prüfen** — vergleicht Manifest-Version mit
  `getModByIdOrReference.latest_version`
- **⬆ Alle updaten** — Bulk-Update aller Mods mit verfügbarem Update,
  sequenziell (throttled 250 ms)
- **🔍 Details** per Doppelklick oder Button
- Ordner-öffnen, Deinstallieren

### Einstellungen-Tab
- Katalog-Cache-Refresh-Intervall (Default 24 h)
- Standard-Sortierung (popularity / hotness / downloads / views / update-date)
- Kein API-Key nötig

### IUpdateNotifier
Grüner ↑-Badge auf der Satisfactory-Kachel **nur bei echten Updates für
installierte Mods** (v0.5.1). Auto-Check läuft 15 s nach Plugin-Load im
Hintergrund. Neue ficsit-Katalog-Einträge sind ein Community-News-Signal
und werden bewusst nicht mehr im Actionable-Badge summiert.

## API

Nutzt die öffentliche **ficsit.app GraphQL-API v2**
(`https://api.ficsit.app/v2/query`). Kein OAuth für Read-Queries, kein
API-Key erforderlich. Direct-Download-URLs (`version.link`) funktionieren
für alle User.

Auf Linux via Steam-Proton läuft die Windows-Variante der Mods (Satisfactory
ist nativ Windows-only; Proton übersetzt) — deshalb `/Windows/download`
als Direct-URL.

## Installation

Aus [Release](https://github.com/KroModIx/KroModIx.Plugin.Satisfactory/releases)
das ZIP entpacken nach:

- **Linux:** `~/.config/KroModIx/plugins/kroste.satisfactory/`
- **Windows:** `%APPDATA%\KroModIx\plugins\kroste.satisfactory\`

Alternativ: 1-Klick-Install über die Install-Karte in der KroModIx-Sidebar.

## Referenz

- **SMM (Satisfactory Mod Manager):** [`satisfactorymodding/SatisfactoryModManager`](https://github.com/satisfactorymodding/SatisfactoryModManager)
- **ficsit-cli:** [`satisfactorymodding/ficsit-cli`](https://github.com/satisfactorymodding/ficsit-cli)
  — Referenz für die GraphQL-Queries

## Lizenz

MIT — siehe [LICENSE](LICENSE).

---

☕ [buymeacoffee.com/kroste](https://buymeacoffee.com/kroste)
