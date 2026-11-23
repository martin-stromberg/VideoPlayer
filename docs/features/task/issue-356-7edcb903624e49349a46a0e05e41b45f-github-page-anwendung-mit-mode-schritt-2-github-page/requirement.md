# Übersetzte Anforderung: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment

**Projekt:** Issue #356, Schritt 2 des Projektplans (`docs/projects/task/issue-356-…/project-plan.md`)
**Branch:** `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode-schritt-2-github-page`
**Basisbranch:** `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`

## Fachliche Zusammenfassung

Das Repository erhält eine statische, öffentlich erreichbare Präsentationsseite unter `https://martin-stromberg.github.io/VideoPlayer/`, die VideoWebPlayer vorstellt. Es handelt sich um eine einzelne Landing-Page auf Deutsch mit Anker-Abschnitten, modernem responsivem Design in heller und dunkler Darstellung sowie einer Screenshot-Galerie mit echten Screenshots aus der lokalen Demoinstallation (Schritt 1, bereits gemergt). Die Veröffentlichung erfolgt über einen neuen GitHub-Actions-Workflow nach GitHub Pages (Artefakt-Deployment), der bei Push auf `main` mit Änderungen unter `pages/` sowie manuell auslöst und GitHub Pages beim ersten Lauf selbst aktiviert.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die **Projektseite** — eigenständiger statischer Website-Quellordner `pages/` (HTML, CSS, JS, Bild-Assets inkl. Screenshots) plus Deployment-Pfad über GitHub Actions nach GitHub Pages plus Pflege-Dokumentation. Die bestehende `docs/`-Dokumentation ist kein veröffentlichter Seiteninhalt.
- **Auslösende Ereignisse:** `push` auf `main` mit Änderungen unter `pages/` (gelangt über den Staging-Pfad Schritt-Branch → Projekt-Branch → `staging` → `main` dorthin) sowie manueller `workflow_dispatch`.
- **Erwartete Wirkung:** Öffentlich erreichbare, responsive Seite unter `https://martin-stromberg.github.io/VideoPlayer/` in heller und dunkler Darstellung — angelehnt an das Erscheinungsbild der Anwendung (`--vp-*`-Farbwelt: Primär `#e50914`, Sekundär `#00a8e8`, dunkler Hintergrund `#131313`) — mit allen geforderten Inhalten: Zweck/Vorstellung, zentrale Features, Screenshot-Galerie mit echten Screenshots aus der Demoinstallation, statischer Release-Verweis, Verweise auf Repository und Dokumentation.
- **Umgang mit fehlenden Objekten:**
  - GitHub-Pages-Aktivierung (in der Vorarbeit per `gh api` verifiziert **nicht** gesetzt, `has_pages: false`): **aktivieren** — primär automatisch über `actions/configure-pages` beim ersten `main`-Lauf, Fallback manuell durch den Eigentümer (Settings → Pages → Source: GitHub Actions); externe Voraussetzung, dokumentierter Fallback.
  - `github-pages`-Environment: wird von `actions/deploy-pages` selbst **erzeugt** — keine Vorarbeit.
  - Seiten-Quellen (`pages/`): existieren nicht — **erzeugen**.
  - Screenshots: existieren nicht — **erzeugen** in einer lokalen Demoinstallation des geklonten Projekts (Demodatenbestand aus Schritt 1: `VideoWebPlayer/DemoDataSets/Demobibliothek/`), als optimierte, versionierte Bilddateien unter `pages/assets/screenshots/` ablegen.
  - Logo/Favicon für die Seite: **wiederverwenden** (`Images/app.png`, `VideoWebPlayer/wwwroot/favicon.png` — in `pages/` kopieren, da das veröffentlichte Artefakt nur `pages/` enthält).
- **Scope-Ausschlüsse:** siehe unten — alle bereits vom Anwender bestätigt.

## Anwender-bestätigte Entscheidungen (verbindlich)

| # | Entscheidung |
|---|--------------|
| 1 | Seiten-Quellen in eigenem Root-Ordner `pages/` |
| 2 | Deployment nur von `main` (kein Vorschau-Deployment); Veröffentlichung folgt dem Staging-Pfad |
| 3 | Sprache der Seite: Deutsch |
| 4 | Umfang: einzelne Landing-Page mit Anker-Abschnitten |
| 5 | Release-Verweis: statischer Link auf die Releases-Übersicht (`https://github.com/martin-stromberg/VideoPlayer/releases`) |
| 6 | Hell/Dunkel: `prefers-color-scheme` als Standard plus manueller Umschalter mit `localStorage`-Persistenz |
| 7 | Keine `docs/`-Veröffentlichung, keine dynamischen Inhalte, keine Custom Domain, keine Suche, keine I18n, kein Site-Generator, keine Release-Prozess-Änderung |
| 8 | Screenshots aus lokaler Demoinstallation des geklonten Projekts |

## Fachliche Anforderungen

- F-1: Eine statische Landing-Page (Deutsch) mit Anker-Abschnitten: prägnante Vorstellung/Zweck, zentrale Features, Screenshot-Galerie, Download-/Release-Verweis (statischer Link auf die Releases-Übersicht), Verweise auf Repository und Dokumentation.
- F-2: Responsives, modernes Design mit klarer Typografie und konsistentem Stil; helle und dunkle Darstellung; an das Erscheinungsbild der Anwendung angelehnt (`--vp-*`-Palette: `#e50914`/`#00a8e8`/dunkles `#131313`; System-Font-Stack, keine externen Webfonts).
- F-3: Theme-Umschaltung: `prefers-color-scheme` als Default plus manueller Umschalter mit `localStorage`-Persistenz.
- F-4: Seiten-Quellen unter `pages/`; alle internen Pfade relativ bzw. basis-pfadfest (Seite läuft unter `/VideoPlayer/`); kein Site-Generator, keine dynamisch erzeugten Inhalte; `.nojekyll` nicht nötig (Artefakt-Deployment).
- F-5: Deployment über GitHub Actions (Pages-Artefakt-Deployment), Trigger `push` auf `main` mit `paths`-Filter `["pages/**"]` plus `workflow_dispatch`; Workflow-Konventionen des Repos einhalten (Header-Kommentar, minimale `permissions`: `contents: read`, `pages: write`, `id-token: write`; `concurrency`-Gruppe `pages` mit `cancel-in-progress: false`; `timeout-minutes`; `ubuntu-latest`; Action-Majors an Repo-Konvention und offizieller Doku).
- F-6: Pflege-Dokumentation im Repository (`docs/GITHUB_PAGE.md`): Inhalte ändern, Screenshots aus der Demoinstallation erneuern inkl. Aufnahme-Verfahren, Deployment-Pfad inkl. Pages-Aktivierung und manuellem Fallback; verlinkt aus `docs/INDEX.md` und `README.md`; `docs/STAGING_WORKFLOW.md` (CI-Workflow-Liste) ergänzen; Eintrag in `docs/RELEASE_NOTES.md` gemäß Projektkonvention (infrastructure only, Issue #356).

## Betroffene Artefakte

- `pages/` (neu): `index.html`, `assets/css/site.css`, `assets/js/theme.js`, `assets/img/` (Logo-/Favicon-Kopien), `assets/screenshots/` (echte Screenshots aus der Demoinstallation).
- `.github/workflows/pages.yml` (neu): Pages-Build/Deploy (Jobs `build` und `deploy`; Actions: `checkout`, `configure-pages`, `upload-pages-artifact` mit `path: pages/`, `deploy-pages`; Environment `github-pages` mit `url`).
- Dokumentation: `docs/GITHUB_PAGE.md` (neu), `docs/INDEX.md`, `README.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md`.
- Keine C#-Produktcode-Änderung; keine Site-Tests im Solution-Scope; Absicherung über `MarkdownLinkCheck`/SecretScan (Pre-Commit-Hooks + CI), Workflow-Definition und lokale Sichtprüfung.

## Akzeptanzkriterien

- AK-1: Die Seite ist unter `https://martin-stromberg.github.io/VideoPlayer/` öffentlich erreichbar (Nachweis erst nach Erreichen von `main` möglich; zuvor lokale Sichtprüfung hell/dunkel und schmaler Viewport).
- AK-2: Design responsiv/modern; helle und dunkle Darstellung; Umschalter mit persistenter Auswahl; Systemeinstellung als Standard.
- AK-3: Alle Inhalte vorhanden: Zweck, zentrale Features, Screenshot-Galerie mit echten Screenshots aus der Demoinstallation, statischer Release-Verweis, Verweise auf Repository und Dokumentation.
- AK-4: Workflow deployt bei Push auf `main` mit `pages/`-Änderungen sowie manuell; Pages-Aktivierung automatisiert bzw. manueller Fallback dokumentiert.
- AK-5: Pflege-Doku vorhanden und verlinkt; Markdown-Link-Prüfung und Repo-Checks grün.

## Scope-Ausschlüsse (alle vom Anwender bestätigt, Issue #356)

- Keine Veröffentlichung der `docs/`-Dokumentation als Website (bestätigt).
- Keine dynamischen Seiteninhalte (keine Build-/Laufzeit-Generierung, keine clientseitige GitHub-API-Anzeige; bestätigt).
- Keine Custom Domain, kein Suchindex, keine Mehrsprachigkeit (bestätigt; Deutsch).
- Kein Site-Generator-Tooling (bestätigt).
- Keine Änderung am Release-Prozess (bestätigt).
- Kein Staging-Vorschau-Deployment der Seite (bestätigt; nur `main`).
- Keine Unterseiten — einzelne Landing-Page (bestätigt).
- Kein `.nojekyll` — nicht nötig bei Artefakt-Deployment (aus Anforderung ableitbar).
- Keine Aktivierung von GitHub Pages in diesem Schritt — erfolgt beim späteren `main`-Lauf (aus Auftragsvorgabe ableitbar).

## Offene Fragen

Keine — alle Entscheidungen sind durch die Anforderung bzw. die anwender-bestätigten Vorgaben abgedeckt. Ableitbare Einzelheiten (Seitentexte, Screenshot-Auswahl, Workflow-Details) werden in der Planung dokumentiert.
