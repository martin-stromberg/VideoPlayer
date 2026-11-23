# Bestandsaufnahme-Detail: Dokumentationslandschaft und Link-Prüfung

## Doku-Einstiegspunkte

| Datei | Rolle | Relevanter Abschnitt |
|-------|-------|----------------------|
| `docs/INDEX.md` | Zentrale Doku-Übersicht (Tabellen „Einstieg", „Technische Dokumentation", „Benutzer- und Admin-Hilfen") | Neue `docs/GITHUB_PAGE.md` gehört in eine der Tabellen (technische Dokumentation) |
| `README.md` | Projekt-Readme (Deutsch): Funktionen, Schnellstart, Erste Schritte, Konfiguration, u. a. | Kurzer Passus zur GitHub-Page mit Link auf die öffentliche URL |
| `docs/STAGING_WORKFLOW.md` | Staging-/CI-Dokumentation | Abschnitt „CI Workflow Files" (Zeilen ~246–259) listet jeden Workflow einzeln — `pages.yml` muss ergänzt werden |
| `docs/RELEASE_NOTES.md` | Release Notes, zweisprachig (EN zuerst, dann DE), Abschnitte „Important Notes Before Update" / „What's New" / „Wichtige Hinweise vor dem Update" / „Neuerungen" | Infrastruktur-Einträge ohne Anwenderänderung stehen nach Konvention (Issue #354, #368) unter „Important Notes"/„Wichtige Hinweise" mit dem Muster „(infrastructure only, Issue #NNN)" |
| `docs/help/` | Anwender-/Admin-Hilfe (z. B. `einrichtung.md` enthält die Demoinstallation aus Schritt 1) | Die Pflege-Doku der Page ist Repo-/Betreiber-Doku, keine Anwenderhilfe — Platz: `docs/GITHUB_PAGE.md`, verlinkt aus `docs/INDEX.md` (kein `docs/help/`-Eintrag nötig; es gibt kein Anwender-Feature) |

## Prüfinfrastruktur

- **MarkdownLinkCheck:** `tools/MarkdownLinkCheck` (eigenes Tool,
  `dotnet run --project tools/MarkdownLinkCheck -- --root .`); läuft im
  Pre-Commit-Hook über alle Markdown-Dateien (letzter Lauf: 460 Dateien,
  330 lokale Links). Neue `.md`-Dateien und neue Links müssen diese Prüfung
  bestehen — relative Links müssen auf existierende Ziele zeigen.
- **Pre-Commit-Hooks** (`.githooks/pre-commit`, konfiguriert via
  `core.hooksPath`): Translation-Check (resx), csproj/.cs-XML-Doc-Check,
  Razor-Localization, Razor-Usage, Stub-Check, Enum-Coverage, SecretScan,
  MarkdownLinkCheck. Für HTML/CSS/JS/YAML greifen davon SecretScan und
  MarkdownLinkCheck (nur .md).
- **SecretScan:** repo-eigener Scan (läuft im Hook; Ausgabe „Secret scan
  passed") — keine Secrets in `pages/` oder `pages.yml` einbauen.

## Konventionen für Doku-Dateien

- Deutsche Dokumentation; Dateinamen in Großbuchstaben-Muster
  (`GUIDE_Installation.md`, `INSTALL_Server.md`, `TECH_*.md`,
  `REVIEW_AUTOMATION.md`, `SECRETS_MANAGEMENT.md`) → `docs/GITHUB_PAGE.md`
  passt zum Muster.
- `docs/INDEX.md` nutzt Tabellen mit `| Dokument | Beschreibung |`-Spalten.
- Release-Notes-Einträge: ein Bullet je Änderung, englisch und deutsch
  deckungsgleich, neueste zuerst.

## Lücken

- `docs/GITHUB_PAGE.md` existiert nicht; `INDEX.md`, `README.md`,
  `STAGING_WORKFLOW.md`, `RELEASE_NOTES.md` enthalten noch keinen
  GitHub-Page-Verweis.
