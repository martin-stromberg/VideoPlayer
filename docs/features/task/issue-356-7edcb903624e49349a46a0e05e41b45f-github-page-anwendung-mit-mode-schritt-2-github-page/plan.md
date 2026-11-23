# Umsetzungsplan: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment (Schritt 2, Issue #356)

## Übersicht

Aufbau einer statischen, deutschsprachigen Landing-Page unter `pages/` (reines
HTML/CSS + kleines JS für den Theme-Umschalter, kein Generator), die
VideoWebPlayer mit Zweck, zentralen Features, echter Screenshot-Galerie aus der
Demoinstallation, Release- und Repo-/Doku-Verweisen vorstellt — hell und dunkel
(System-Default + persistenter manueller Umschalter). Dazu ein neuer
GitHub-Actions-Workflow `pages.yml` für das Artefakt-Deployment nach GitHub
Pages (Trigger: `push` auf `main` mit `paths`-Filter auf `pages/**` plus
`workflow_dispatch`) sowie die Pflege-Dokumentation `docs/GITHUB_PAGE.md` mit
Verlinkung aus den bestehenden Doku-Einstiegspunkten.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die **Projektseite**: statischer Quellordner `pages/` (HTML, CSS, JS, Bild-Assets inkl. Screenshots) + Deployment-Workflow + Pflege-Dokumentation |
| Auslösende Ereignisse | `push` auf `main` mit Änderungen unter `pages/` (via Staging-Pfad); manueller `workflow_dispatch` |
| Erwartete Wirkung | Öffentlich erreichbare, responsive Seite unter `https://martin-stromberg.github.io/VideoPlayer/` in heller und dunkler Darstellung mit allen geforderten Inhalten |
| Umgang mit fehlenden Objekten | GitHub-Pages-Aktivierung: **aktivieren** durch `actions/configure-pages` beim ersten `main`-Lauf, manueller Fallback dokumentiert; `github-pages`-Environment: wird von `deploy-pages` **erzeugt**; `pages/`-Quellen: **erzeugen**; Screenshots: **erzeugen** in lokaler Demoinstallation → `pages/assets/screenshots/`; Logo/Favicon: **wiederverwenden** (`Images/app.png` verkleinert, `VideoWebPlayer/wwwroot/favicon.png` kopiert) |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Veröffentlichung der `docs/`-Doku als Website | Präsentationsseite ist eigenes Artefakt | bestätigt (Anwender-Anforderung, Issue #356) |
| 2 | Keine dynamischen Seiteninhalte (keine Build-/Laufzeit-Generierung, keine GitHub-API-Aufrufe im Client) | Bestätigte Entscheidung | bestätigt (Anwender-Anforderung, Issue #356) |
| 3 | Keine Custom Domain, keine Suche, keine Mehrsprachigkeit | Bestätigte Entscheidung (Deutsch) | bestätigt (Anwender-Anforderung, Issue #356) |
| 4 | Kein Site-Generator (kein Jekyll/SSG), kein `.nojekyll` | Bestätigt; Artefakt-Deployment braucht kein `.nojekyll` | bestätigt (Anwender-Anforderung, Issue #356) |
| 5 | Keine Release-Prozess-Änderung; kein Vorschau-Deployment (nur `main`) | Bestätigte Entscheidung | bestätigt (Anwender-Anforderung, Issue #356) |
| 6 | Keine Unterseiten — einzelne Landing-Page mit Ankern | Bestätigte Entscheidung | bestätigt (Anwender-Anforderung, Issue #356) |
| 7 | Keine Pages-Aktivierung in diesem Schritt (erst beim `main`-Lauf); kein Live-Erreichbarkeits-Nachweis vor `main` | Auftragsvorgabe + externe Bedingung | bestätigt (Anwender-Anforderung, Issue #356) |
| 8 | Keine neue Test-Infrastruktur für `pages/` im Solution-Scope (kein Node-Testprojekt, keine Solution-Tests für statische Assets) | Statische Assets ohne Logik-Backend; Absicherung via MarkdownLinkCheck, SecretScan, lokaler E2E-Abnahme mit Playwright | bestätigt (Anwender-Anforderung, Issue #356 — „kein Site-Generator, keine dynamisch erzeugten Inhalte") |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Seitenstruktur | Eine `index.html` mit semantischen Anker-Abschnitten (`#features`, `#screenshots`, `#download`, `#links`), Header mit Logo+Ankern+Theme-Umschalter, Hero, Footer | Bestätigt: einzelne Landing-Page, Deutsch |
| Styling | `assets/css/site.css` mit zwei Token-Sätzen: dunkle Palette = `--vp-*`-Werte der Anwendung; helle Palette abgeleitet (gleiche Akzente `#e50914`/`#00a8e8`, helle Flächen, dunkle Texte); Theme über `data-theme`-Attribut auf `<html>`; Font-Stack der App (Inter/Montserrat ohne Webfont-Einbindung) | Anforderung „an das Erscheinungsbild angelehnt"; keine externe Font-Dependency (System-Stack) |
| Theme-Umschaltung | Kleines Inline-`<script>` im `<head>` liest `localStorage('vwp-theme')` bzw. `prefers-color-scheme` und setzt `data-theme` **vor** dem ersten Paint (kein FOUC); `assets/js/theme.js` verdrahtet den Umschalter und persistiert; `prefers-color-scheme`-Änderung ohne gespeicherte Wahl folgt dem System | Anforderung B10; FOUC-freier Start erfordert Inline-Bootstrapping |
| Interne Pfade | Ausschließlich relative Pfade (`./assets/…`, `#anker`), kein `<base>`-Tag | Seite läuft unter `/VideoPlayer/` — absolute `/assets/…`-Pfade würden den Projekt-Unterpfad verlieren |
| Screenshots | Aufnahme per Playwright-Node-Skript gegen lokal laufende Demoinstallation (`dotnet run --project VideoWebPlayer`); 4–5 Ansichten (Start/Bibliothek, Medienquelle/Detailansicht, Film- oder Serien-Detail, Wiedergabe/Player, ggf. Live-TV); Desktop-Viewport ~1600×900; Export als WebP (Qualität ~80), Fallback PNG bei Bedarf | Anforderung: echte Screenshots aus der Demoinstallation, optimiert und versioniert |
| Logo-Variante | `Images/app.png` (1024², 1,37 MB) auf 512×512 WebP + 256×256 PNG-Fallback verkleinern; `favicon.png` 1:1 kopieren | Repo-Logo ist zu groß für direkte Webauslieferung |
| Galerie-Darstellung | Responsive Karten-/Grid-Galerie mit `<img loading="lazy">`, `alt`-Texten, ggf. `<a>` auf Vollbild | Modernes, responsives Design; Barrierefreiheit |

## Programmabläufe

### Veröffentlichung der Seite

1. Änderung an `pages/` gelangt über den Staging-Pfad (Schritt-Branch → Projekt-Branch → `staging` → `main`) auf `main`.
2. `push`-Trigger (mit `paths`-Filter `pages/**`) startet `pages.yml`: Job `build` (`checkout` → `configure-pages` (aktiviert Pages beim ersten Lauf) → `upload-pages-artifact` mit `path: pages/`); Job `deploy` (`deploy-pages`, Environment `github-pages`, Ausgabe `page_url`).
3. Seite ist unter `https://martin-stromberg.github.io/VideoPlayer/` erreichbar. `workflow_dispatch` erlaubt manuelle Neuveröffentlichung.
4. Falls `configure-pages` die Aktivierung nicht übernimmt: dokumentierter Fallback (Settings → Pages → Source: „GitHub Actions"), danach `workflow_dispatch`.

### Screenshot-Produktion und Seitenpflege

1. `dotnet run --project VideoWebPlayer` lokal starten (frische `Data/`-Installation).
2. Über Playwright: `Account/Register` mit `Demobibliothek` registrieren → `RegisterConfirmation` Confirm-Link → `ConfirmEmail` → `Login` → Bibliothek gefüllt (Scan läuft sofort, ggf. kurz warten).
3. Definierte Ansichten aufrufen, Screenshots aufnehmen, als WebP unter `pages/assets/screenshots/` ablegen, in der Galerie verdrahten.
4. Inhaltspflege erfolgt per Commit über den Staging-Pfad; Verfahren in `docs/GITHUB_PAGE.md` beschrieben.

### Theme-Umschaltung (Nutzerfluss)

1. Beim Laden: `data-theme` = gespeicherte Wahl (`localStorage`) oder System (`prefers-color-scheme`).
2. Klick auf Umschalter → `data-theme` wechselt, Wahl wird in `localStorage` persistiert.
3. Nach Reload: gespeicherte Wahl greift weiterhin; ohne gespeicherte Wahl folgt die Seite Systemwechseln.

## Neue Dateien

| Datei | Zweck |
|-------|-------|
| `pages/index.html` | Landing-Page (Deutsch, Anker-Abschnitte) |
| `pages/assets/css/site.css` | Design-Tokens hell/dunkel, Layout, responsives Grid |
| `pages/assets/js/theme.js` | Theme-Umschalter mit `localStorage`-Persistenz |
| `pages/assets/img/logo.webp` (+ `logo.png`-Fallback) | Verkleinertes App-Logo |
| `pages/assets/img/favicon.png` | Favicon-Kopie |
| `pages/assets/screenshots/*.webp` | 4–5 echte Screenshots aus der Demoinstallation |
| `.github/workflows/pages.yml` | Pages-Build/Deploy-Workflow |
| `docs/GITHUB_PAGE.md` | Pflege-Dokumentation (Inhalte, Screenshots, Deployment inkl. Aktivierung/Fallback) |

## Änderungen an bestehenden Dateien

- `docs/INDEX.md` — Eintrag „GitHub Page" in der Tabelle „Technische Dokumentation".
- `README.md` — kurzer Passus zur öffentlichen Präsentationsseite (mit Link auf die Live-URL und auf `docs/GITHUB_PAGE.md`).
- `docs/STAGING_WORKFLOW.md` — `pages.yml` in der Liste „CI Workflow Files" ergänzen.
- `docs/RELEASE_NOTES.md` — Infrastruktur-Eintrag (EN/DE, Muster „infrastructure only, Issue #356").

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine (keine Eingaben; externe Schnittstelle ist nur der Workflow — syntaktisch korrekte YAML, Pflichtfelder `environment`/`permissions` gemäß Action-Verträgen).

## Konfigurationsänderungen

Keine Laufzeitkonfiguration. Externe Einstellung: GitHub-Pages-Quelle auf Repository-Ebene — primär automatisch via `configure-pages`, Fallback manuell dokumentiert.

## Seiteneffekte und Risiken

- **CI-Workflow-Liste:** `pages.yml` triggert nicht auf `staging`/PRs — kein Einfluss auf bestehende Checks. Der Workflow wird erst auf `main` aktiv; die MarkdownLinkCheck-Prüfung der neuen Doku läuft lokal per Pre-Commit-Hook.
- **`Data/`-Laufzeitverzeichnis:** Die lokale Demoinstallation für Screenshots legt `Data/` unter `VideoWebPlayer/` an (gitignored). Während des `dotnet run`-Laufs keine OsInterface-Tests parallel ausführen (Demo-Media-Cleanup-Effekt aus Schritt 1).
- **Bildgrößen:** Unoptimierte Screenshots würden das Repo aufblähen — WebP-Export mit Größenbudget (Ziel: < ~300 KB pro Bild).

## Umsetzungsreihenfolge

1. **`pages/` Grundgerüst:** `index.html`, `assets/css/site.css`, `assets/js/theme.js` mit allen Abschnitten (Screenshot-Platzhalter-Verdrahtung auf die späteren Dateinamen).
   - Voraussetzungen: keine.
2. **Bild-Assets:** `favicon.png` kopieren; `logo.webp`/`logo.png` aus `Images/app.png` verkleinern (Python/Pillow).
   - Voraussetzungen: Schritt 1 (Dateipfade).
3. **Demoinstallation hochfahren + Screenshots:** App lokal starten, Erstregistrierung mit `Demobibliothek` per Playwright, Ansichten aufnehmen, WebP-Export nach `pages/assets/screenshots/`, `index.html`-Galerie finalisieren.
   - Voraussetzungen: Schritt 1–2; .NET-Build der Anwendung; Node/Playwright (vorhanden).
4. **Workflow `.github/workflows/pages.yml`** nach Repo-Konvention.
   - Voraussetzungen: keine.
5. **Dokumentation:** `docs/GITHUB_PAGE.md`, `docs/INDEX.md`, `README.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md`.
   - Voraussetzungen: Schritt 3 (Screenshot-Dateinamen für die Doku), Schritt 4 (Workflow-Name für Verweise).

## Tests

### Neue Tests

Keine Solution-Tests (statische Seite ohne Produktcode-Änderung; Scope-Ausschluss 8). Absicherung stattdessen:

| Prüfung | Werkzeug | Was wird geprüft |
|---------|----------|-------------------|
| Markdown-Link-Prüfung | Pre-Commit-Hook (`tools/MarkdownLinkCheck`) | Alle neuen Doku-Links zeigen auf existierende Ziele |
| SecretScan | Pre-Commit-Hook | Keine Secrets in `pages/`/`pages.yml` |
| Lokale E2E-Abnahme | Playwright-Node-Skript (Akzeptanz-Artifact im Feature-Verzeichnis) | Seite lädt, alle Anker-Abschnitte vorhanden, Galerie-Bilder laden (`naturalWidth > 0`), Theme-Umschalter persistiert, kein horizontaler Überlauf bei 375px |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung berührt einen Benutzerfluss außerhalb der .NET-Solution (die statische Seite). E2E-Nachweis erfolgt über die **reale End-to-End-Abnahme** mit Playwright gegen die lokal ausgelieferte Seite (statischer HTTP-Server auf `pages/`, Basis-Pfad `/VideoPlayer/` simuliert), dokumentiert in `test-results.md`:

| Priorität | Szenario | Ausführung | Abgedecktes Akzeptanzkriterium |
|-----------|----------|------------|-------------------------------|
| Pflicht | Seite laden → alle Inhalte sichtbar (Zweck, Features, Galerie mit geladenen echten Screenshots, Release-/Repo-/Doku-Links), Desktop-Viewport | Playwright-Skript + Screenshots | AK-3 |
| Pflicht | Helle und dunkle Darstellung; Umschalter toggelt und persistiert über Reload; ohne gespeicherte Wahl folgt die Seite `prefers-color-scheme` | Playwright (emulierte `colorScheme`, `localStorage`-Prüfung, Reload) + Screenshots hell/dunkel | AK-2 |
| Pflicht | Schmaler Viewport (375px): responsives Layout ohne horizontalen Überlauf | Playwright-Screenshot + `scrollWidth`-Check | AK-2 |

Begründung, warum keine Solution-E2E-Tests: `pages/` ist ein statisches Artefakt ohne Laufzeitlogik im Produkt; eine Node-Testinfrastruktur im Repo würde dem bestätigten Ausschluss „kein Site-Generator/keine dynamischen Inhalte" widersprechen. Die Playwright-Abnahme liefert den realen Benutzerfluss-Nachweis.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Veröffentlichung | Push `pages/**` auf `main` (erreicht via Staging-Pfad) | Pages noch nicht aktiviert (`has_pages: false`), kein `github-pages`-Environment | Workflow läuft: `configure-pages` aktiviert Pages, Artefakt-Upload, `deploy-pages` deployt | Seite öffentlich unter `/VideoPlayer/` erreichbar | Workflow-Definition + dokumentierter Nachweis beim main-Lauf; **in diesem Schritt nicht ausführbar** → lokale Abnahme als Ersatznachweis, Live-Nachweis verbleibt beim main-Lauf (siehe Scope-Ausschluss 7) |
| 2 | Lokale Sichtprüfung (Ersatz für #1 bis main) | Statischer Server auf `pages/` | Seite existiert lokal | Seite laden, hell/dunkel umschalten, 375px-Viewport | Alle Inhalte, beide Themes, responsiv | Playwright-Screenshots + E2E-Checks |
| 3 | Theme-Persistenz | Klick auf Umschalter | Keine gespeicherte Wahl, System = dunkel | Toggle → hell, Reload | Seite bleibt hell nach Reload (`localStorage`) | Playwright-Check + Screenshot |
| 4 | Galerie-Echtheit | Seite laden | Screenshots aus Demoinstallation unter `pages/assets/screenshots/` | Bilder laden vollständig | Echte App-Ansichten sichtbar, kein Platzhalter/404 | `naturalWidth`-Check + Sichtprüfung |
| 5 | Fehlerfall Pages nicht auto-aktiviert | `configure-pages` kann Pages nicht aktivieren | `has_pages: false` + Aktivierung scheitert | Manueller Fallback aus `docs/GITHUB_PAGE.md` (Settings → Pages → „GitHub Actions"), dann `workflow_dispatch` | Deployment läuft nach manueller Aktivierung | Dokumentierter Pfad (nicht im Schritt ausführbar — externe Bedingung) |

## Offene Punkte

Keine.
