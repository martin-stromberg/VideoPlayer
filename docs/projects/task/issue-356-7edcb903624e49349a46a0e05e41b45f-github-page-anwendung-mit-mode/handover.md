# Handover – Projekt GitHub Page mit Demoinstallation

Projektbranch: `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`
Issue: martin-stromberg/VideoPlayer#356

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin | `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode-schritt-1-demoinstallation` | aktiv | 2026-11-23 |

## Übergabestand je Schritt

### Schritt 1: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

- **Stand vom:** 2026-11-23, Schritt-Branch noch nicht erzeugt
- **Bearbeiter / Übernehmer:** Devin / offen

**Erledigt**
- Projektartefakte: `requirement.md`, `inventory.md` (+Detaildokumente), `project-plan.md`, `project-plan-check.md` (Status: Projektplan vollständig), `steps.md`.
- Anwender-Entscheidungen: Empfehlungen zur Page bestätigt; Scope-Erweiterung (Demoinstallation, lokale Quelle, Screenshots aus Demoinstallation) per Issue-Kommentar.

**Offen**
- Schritt-Branch anlegen, Lifecycle komplett durchlaufen, unabhängige Abnahme, Merge.

**Entscheidungen**
- Offene Planpunkte 1–4 behandelt: Dummy-mp4 beilegen, Anzeigebilder per SkiaSharp zur Laufzeit, 2–3 Filme + 1–2 Serien à 2–3 Episoden, bestehendes Fehlerverhalten bei ApplyAsync behalten.
- `MediaSourceType.LocalDirectory` ist bereits released — kein eigener Schritt, nur Voraussetzung.

**Außerhalb des Auftrags**
- Keine.

**Tests**
- Baseline: 2969/2973 grün, 2 dokumentierte OsInterface-E2E-Flakes (Retry grün), 2 begründete Skips; Fokuslauf Demo/MediaSource 120/120 grün.

**Achtung**
- `DemoDataSets/` und `StandardSources.json` existieren noch nicht; csproj-Regel kopiert bisher nur `DemoDataSets\*.json` — Medien-Assets in Unterordnern gehen beim Publish sonst verloren.
- EF-Migrationen kollidieren im Model-Snapshot: bei späterem Merge nach Rebase neu erzeugen (siehe AGENTS.md).
