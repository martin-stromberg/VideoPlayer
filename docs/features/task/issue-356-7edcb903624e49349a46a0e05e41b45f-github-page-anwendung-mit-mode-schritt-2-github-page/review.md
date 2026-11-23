# Review: Plan ↔ Umsetzung (Schritt 2 — GitHub Page)

**Status:** Vollständig umgesetzt

Diff-Basis: `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`

## Prüfung Plan → Umsetzung

| Planpunkt | Umsetzung | Ergebnis |
|-----------|-----------|----------|
| `pages/index.html` — deutsche Landing-Page mit Anker-Abschnitten | Vorhanden; Header, Hero, `#features`, `#screenshots`, `#download`, `#links`, Footer; alle Texte deutsch; `lang="de"` | OK |
| `pages/assets/css/site.css` — zwei Token-Sätze (dunkel = `--vp-*`-Werte der App, hell abgeleitet), `data-theme` auf `<html>` | Vorhanden: dunkle Palette mit `#131313`, `#e50914`, `#00a8e8` u. a.; helle Palette abgeleitet; `color-scheme` je Theme; responsives Grid | OK |
| `pages/assets/js/theme.js` + Inline-FOUC-Script — System-Default, manueller Umschalter, `localStorage('vwp-theme')` | Inline-Script im `<head>` setzt `data-theme` vor erstem Paint; `theme.js` toggelt + persistiert + folgt Systemwechsel ohne gespeicherte Wahl | OK |
| Relative Pfade, kein `<base>` | Alle internen Verweise `./assets/…`, `#anker`; kein `<base>`-Tag | OK |
| `pages/assets/img/` — Logo (aus `Images/app.png` verkleinert: `logo.webp` + `logo.png`) und `favicon.png` | Vorhanden: `logo.webp` (512²), `logo.png` (256²), `favicon.png` (64²) | OK |
| `pages/assets/screenshots/*.webp` — 4–5 echte Screenshots aus Demoinstallation | 5 echte Aufnahmen (Playwright, lokale Demoinstallation, `Demobibliothek`): `startseite`, `medienquelle`, `sammlung`, `seriendetails`, `wiedergabe` — WebP, 19–43 KB | OK (Abweichung siehe unten) |
| `.github/workflows/pages.yml` — Trigger `push`/`main` + `paths: pages/**` + `workflow_dispatch`; Jobs `build` (checkout → configure-pages → upload-pages-artifact `path: pages/`) + `deploy` (deploy-pages, Environment `github-pages`, `page_url`); minimale Rechte, Concurrency `cancel-in-progress: false`, `ubuntu-latest`, Timeouts | Vollständig umgesetzt: `contents: read`/`pages: write`/`id-token: write`; `configure-pages@v6`, `upload-pages-artifact@v5`, `deploy-pages@v5`, `checkout@v7`; Deploy-Job mit eigenem `permissions`-Block und `environment.url` | OK |
| `docs/GITHUB_PAGE.md` + Verlinkung in `docs/INDEX.md`, `README.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md` | Alle fünf Dokumente angelegt/ergänzt (siehe Diff) | OK |
| Keine Solution-Tests für `pages/` (Scope-Ausschluss 8), Absicherung via Pre-Commit + lokaler E2E-Abnahme | Keine neuen Solution-Tests; Playwright-Abnahme-Skript gegen statisch ausgelieferte `pages/` unter simuliertem Basispfad `/VideoPlayer/` | OK |

## Abweichungen vom Plan

| # | Plan | Tatsächliche Umsetzung | Bewertung |
|---|------|------------------------|-----------|
| 1 | Screenshot-Set „ggf. Live-TV" | Live-TV-Screenshot **entfällt**: Der TV-Sender ist in der Demoinstallation nicht freigeschaltet (`/live-tv` zeigt nur „Der TV-Sender ist nicht freigeschaltet."), eine Freischaltung über die Admin-UI scheiterte in der Aufnahme-Session am verschachtelten Formular-Submit. Stattdessen liefern die beiden zusätzlichen echten Ansichten `medienquelle` und `sammlung` die geplanten 4–5 Galerie-Items (jetzt 5). | Unproblematisch — Plan sah Live-TV nur optional vor („ggf."), Feature ist in der Feature-Liste dennoch beworben. In `docs/GITHUB_PAGE.md` als Hinweis dokumentiert. |

## Nicht eintretbare Planpunkte in diesem Schritt

- Szenario 1/5 (Pages-Aktivierung/Live-Nachweis) ist per Scope-Ausschluss 7 erst beim `main`-Lauf möglich — dokumentierter Ersatznachweis: lokale Abnahme (`test-results.md`).

## Ergebnis

Alle planmäßigen Artefakte sind umgesetzt; die einzige Abweichung ist inhaltlich abgedeckt und dokumentiert.
