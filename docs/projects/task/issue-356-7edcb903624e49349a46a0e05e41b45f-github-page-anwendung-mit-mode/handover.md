# Handover – Projekt GitHub Page mit Demoinstallation

Projektbranch: `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`
Issue: martin-stromberg/VideoPlayer#356

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin | `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode-schritt-1-demoinstallation` | pausiert | 2026-11-23 |
| 2 | Devin | `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode-schritt-2-github-page` | fertig | 2026-11-23 |

## Übergabestand je Schritt

### Schritt 1: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

- **Stand vom:** 2026-11-23, Implementierung abgeschlossen und committed; unabhängige Abnahme steht noch aus
- **Bearbeiter / Übernehmer:** Devin / offen (Abnahme)

**Erledigt**
- Projektartefakte: `requirement.md`, `inventory.md` (+Detaildokumente), `project-plan.md`, `project-plan-check.md` (Status: Projektplan vollständig), `steps.md`.
- Anwender-Entscheidungen: Empfehlungen zur Page bestätigt; Scope-Erweiterung (Demoinstallation, lokale Quelle, Screenshots aus Demoinstallation) per Issue-Kommentar.
- Lifecycle vollständig durchlaufen: Anforderung, Bestandsaufnahme, Planung, Plan-Gegenprüfung, Implementierung, Plan-/Usability-/Code-Review, Tests inkl. realer E2E-Abnahme mit Playwright und Sichtprüfung der Screenshots, Dokumentation (help/README/RELEASE_NOTES). Feature-Verzeichnis mit Abschluss-Commit gelöscht; `…-tasks.md` enthält Ergebnis-Tabelle.
- Umgesetzt: `FileSystemDemoDataSetService` (erweitert um `LocalDirectory`-Quellen, Pfad-Validierung, sofortiger Scan via `MediaSourceScanCoordinator`), `DemoMediaContentMaterializer` + `DemoMediaImageGenerator` (SkiaSharp-JPEGs), versionierter Bestand `Demobibliothek` unter `VideoWebPlayer/DemoDataSets/` (3 Filme, 2 Serien à 1 Staffel/3 Episoden, NFOs, h264-MP4 `demo-video.mp4`), Auswahl im unveränderten Registrierungsdialog, `MediaSourceUser`-Zugriff für Erstbenutzer, `.gitignore` für `Data/DemoMedia/`, 12 neue Tests, Doku in `docs/help/einrichtung.md`, README- und Release-Notes-Eintrag (EN/DE).

**Offen**
- Unabhängige Abnahme durch anderen Agenten (`acceptance-schritt-1.md` im Projektordner) — Implementierer darf keine eigene Abnahme schreiben.
- Danach Merge des Schritt-Branches in den Projekt-Branch.

**Entscheidungen**
- Offene Planpunkte 1–4 behandelt: Dummy-mp4 beilegen, Anzeigebilder per SkiaSharp zur Laufzeit, 3 Filme + 2 Serien à 3 Episoden, bestehendes Fehlerverhalten bei ApplyAsync behalten (Benutzer bleibt, Fehler im Formular).
- `MediaSourceType.LocalDirectory` ist bereits released — kein eigener Schritt, nur Voraussetzung.
- Poster-Maß gegenüber Plan von 500×750 auf 800×1200 erhöht (Screenshot-Tauglichkeit).
- Keine Unteragenten in der Umgebung verfügbar — der Orchestrator hat alle Lifecycle-Schritte selbst ausgeführt (im `todo.md` dokumentierte Abweichung).

**Außerhalb des Auftrags**
- Keine.

**Tests**
- Debug- und Release-Build `VideoPlayer.sln`: 0 Fehler.
- Volle Suite (Release): 2983 bestanden, 0 Fehler, 2 begründete Skips (gesamt 2985). Demo-Fokus: 12/12 grün.
- Reale Abnahme `dotnet run` + Playwright: Registrierung mit Demobibliothek → Bestätigung → gefüllte Bibliothek, Quelle, Film-/Serien-Details, Episoden; MP4 über HTTP abrufbar (`ftypisom…avc1`, 134.226 Bytes, `MP4-PLAYABLE: True`). Screenshots sichtgeprüft.
- Bekannter Nebeneffekt: E2E-Test-Dispose räumt `Data/DemoMedia` unter dem Projekt-ContentRoot ab — während parallelem `dotnet run` führt das zu 404 auf Streams (kein Produktbefund).
- Debug-Lauf zeigt bei 7 bestehenden Playwright-Tests `404 /_framework/dotnet-browser-tools/hot-reload-settings.json` (Dev-Only-Middleware-Artefakt, Release-Lauf grün).

**Achtung**
- `DemoDataSets/` und `StandardSources.json` existieren noch nicht; csproj-Regel kopiert bisher nur `DemoDataSets\*.json` — Medien-Assets in Unterordnern gehen beim Publish sonst verloren. → Erledigt: csproj kopiert jetzt `DemoDataSets\**` (JSON + Inhalte) nach Build und Publish; im Output verifiziert.
- EF-Migrationen kollidieren im Model-Snapshot: bei späterem Merge nach Rebase neu erzeugen (siehe AGENTS.md). Dieser Schritt enthält keine Migration.

### Schritt 2: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment

- **Stand vom:** 2026-11-23, Umsetzung abgeschlossen — wartet auf unabhängige Abnahme und Merge
- **Bearbeiter / Übernehmer:** Devin / offen (Abnahme)

**Erledigt**
- Schritt 1 gemergt (Merge `cc016ab`): Demoinstallation komplett inkl. Tests, Doku, Abnahme (`acceptance-schritt-1.md`: Erfüllt). Demobibliothek steht für Screenshots bereit.
- Lifecycle komplett: Anforderung, Bestandsaufnahme, Plan + Gegenprüfung (`Plan vollständig`), Implementierung, Plan-Review (`Vollständig umgesetzt`), Usability-Review (`Keine Befunde`), Code-Review (`Keine Befunde`), Tests (`Keine Fehler`).
- `pages/` angelegt: deutsche Landing-Page (`index.html` mit Ankern `#features`/`#screenshots`/`#download`/`#links`), `assets/css/site.css` (dunkle `--vp-*`-Palette + abgeleitete helle, `data-theme`), `assets/js/theme.js` + Inline-Bootstrap (System-Default, `localStorage('vwp-theme')`, kein FOUC), `assets/img/` (Logo webp/png + favicon), `assets/screenshots/` 5 echte WebP-Aufnahmen aus der lokalen Demoinstallation (startseite, seriendetails, medienquelle, sammlung, wiedergabe; je 19–43 KB).
- `.github/workflows/pages.yml`: `push`→`main` mit `paths: pages/**` + `workflow_dispatch`; `build` (checkout@v7 → configure-pages@v6 → upload-pages-artifact@v5 `path: pages/`) + `deploy` (deploy-pages@v5, `environment: github-pages`, `page_url`); Rechte `contents: read`/`pages: write`/`id-token: write`, Concurrency `cancel-in-progress: false`.
- Doku: `docs/GITHUB_PAGE.md` (Pflege, Screenshot-Verfahren, Deployment + Aktivierungs-Fallback) neu; `docs/INDEX.md`, `README.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md` (EN/DE, „infrastructure only") ergänzt.

**Offen**
- Unabhängige Abnahme und Merge des Schritt-Branches auf den Projekt-Branch.
- Pages-Aktivierung/Live-Nachweis erst beim `main`-Lauf (Scope-Ausschluss 7) — `configure-pages` aktiviert automatisch, manueller Fallback dokumentiert (`docs/GITHUB_PAGE.md`).

**Entscheidungen**
- Alle Page-Entscheidungen vom Anwender bestätigt: `pages/`-Ordner, Deploy nur `main`, Deutsch, Landing-Page, statischer Release-Link, Theme-Toggle mit localStorage.
- `configure-pages` aktiviert Pages beim ersten `main`-Lauf; manueller Fallback wird dokumentiert.
- Galerie ohne Live-TV-Ansicht: TV-Sender ist in der Demoinstallation nicht freigeschaltet (`/live-tv` zeigt nur einen Hinweis); stattdessen Medienquellen- und Filmsammlungs-Ansicht aufgenommen (Plan sah Live-TV nur optional vor). Hinweis in `docs/GITHUB_PAGE.md`.
- Keine Unteragenten verfügbar (Vorgabe): alle Lifecycle-Schritte vom Orchestrator ausgeführt, dokumentiert in `todo.md`.

**Außerhalb des Auftrags**
- Keine.

**Tests**
- Debug-Build 0 Fehler; Release-Build 0 Fehler (145 bekannte Warnungen).
- Volle Suite Release: 2980 grün / 3 Fehlschläge / 2 Skips — die 3 Fehlschläge (BackupUpload-E2E, LiveTv-Warmup-E2E, MediaSearchSelector-bUnit) liefen isoliert 3/3 grün (lastsensitiv, bekannte Flake-Kategorie).
- SecretScan bestanden; MarkdownLinkCheck bestanden (476 Dateien, 342 Links); Pre-Commit-Hooks liefen beim Commit.
- Reale E2E-Abnahme der Seite (lokaler Server, Basispfad `/VideoPlayer/` simuliert, Playwright): 20/20 Checks PASS — Abschnitte, Galerie lädt, Theme-Toggle + Persistenz + System-Default hell/dunkel, 375 px ohne Überlauf, keine JS-Fehler. Alle 5 App-Screenshots einzeln sichtgeprüft.

**Achtung**
- Screenshots müssen aus der Demoinstallation stammen (Anwender-Vorgabe); Basis-Pfad `/VideoPlayer/` bei allen internen Links beachten.
- Kein Push/Merge/PR/Pages-Aktivierung durch den Agenten (Anwender-Vorgabe) — Abschluss-Commit liegt lokal auf dem Schritt-Branch.

**Abschluss:** Beide Schritte fertig und gemergt (Schritt 1 `cc016ab`, Schritt 2 Merge-Commit nach `422caef`). Projekt wird abgeschlossen, Projektverzeichnis wird entfernt — Historie bleibt in den Commits erhalten.
