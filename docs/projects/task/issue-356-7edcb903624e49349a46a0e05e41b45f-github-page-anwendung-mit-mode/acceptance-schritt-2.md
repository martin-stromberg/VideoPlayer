# Abnahmeprüfung – Entwicklungsschritt 2

## Ergebnis

**Status:** Erfüllt

## Abweichungen

- [ ] Keine.

## Hinweise

Unabhängige Abnahme ohne Rückgriff auf den Selbstbericht; alle Nachweise selbst ausgeführt (2026-11-23, frischer Prüferlauf).

**Geprüfter Diff** (`task/...-anwendung-mit-mode...HEAD`): `pages/index.html` (163 Z.), `pages/assets/css/site.css` (446 Z.), `pages/assets/js/theme.js` (56 Z.), `pages/assets/img/` (logo.png, logo.webp, favicon.png), `pages/assets/screenshots/` (5 WebP), `.github/workflows/pages.yml` (63 Z.), `docs/GITHUB_PAGE.md` (neu, 92 Z.), Zeilen-Ergänzungen in `README.md`, `docs/INDEX.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md` (EN+DE).

**Eigene reale Abnahme der Seite** — `pages/` in `serve/VideoPlayer/` kopiert und per `python -m http.server 8090` unter dem Produktiv-Basispfad `/VideoPlayer/` ausgeliefert, Playwright/Chromium:

- Keine fehlgeschlagenen Requests (HTTP ≥ 400) und keine Konsolenfehler; alle 5 Galerie-Bilder laden (`naturalWidth 1600`).
- System-Default greift: `colorScheme: dark` → `data-theme="dark"`, `colorScheme: light` → `data-theme="light"` (frisches Profil, kein gespeicherter Wert).
- Toggle funktioniert und persistiert: Klick → `light`, `localStorage('vwp-theme')='light'`, bleibt nach Reload erhalten; Rücktoggle → `dark`/`'dark'`.
- 375-px-Viewport: `scrollWidth 375 == innerWidth 375`, kein horizontaler Überlauf; Navigation blendet sich aus, Inhalte stapeln sauber (Sichtprüfung eigener Screenshots hell/dunkel/mobil).
- Alle internen Referenzen sind relative Pfade (`./assets/…`, `#anker`) — basispfadfest unter `/VideoPlayer/` verifiziert; kein `<base>`, keine absoluten `/`-Pfade, keine externen Laufzeitabhängigkeiten.
- Externe Links: `github.com/martin-stromberg/VideoPlayer` (+ `/releases`, `/issues`, `/tree/main/docs`, `/blob/main/LICENSE`) — Repo-Remote und LICENSE-Datei im Repo bestätigt; Lizenzangabe „PolyForm Noncommercial 1.0.0" stimmt mit `LICENSE` überein.
- Inhalte vollständig: Zweck/Hero, 6 Feature-Karten, Screenshot-Galerie mit `<figure>`/`figcaption`/`alt`, Release-Verweis (Hero + Download-Sektion), Repo-/Doku-/Issue-Links; `lang="de"`, deutsche Texte.

**Screenshots** — alle 5 Dateien einzeln sichtgeprüft (WebP, 1600×950, 20–45 KB): echte App-Aufnahmen der Demoinstallation — „Martins Videosammlung"-Startseite mit „Neu im Programm", Quelle „Demo-Medienbibliothek" mit Genre-Filtern, Filmsammlung „Filme", Seriendetails „Nebelwerk" (ausdrücklich „erfundene Serie des Demodatenbestands", Staffel 01/3 Episoden), laufende Wiedergabe des Demo-Testvideos. `favicon.png` ist byte-identisch mit `VideoWebPlayer/wwwroot/favicon.png` (MD5 geprüft).

**Workflow `pages.yml`** — YAML syntaktisch valide (PyYAML-Load); Trigger `push`→`main` mit `paths: pages/**` + `workflow_dispatch` (Staging-Pfad: live erst auf `main`); Permissions `contents: read`/`pages: write`/`id-token: write` entsprechen dem offiziellen Starter-Workflow (`actions/starter-workflows/pages/static.yml`); `concurrency.group: pages`, `cancel-in-progress: false`, Timeouts 10 min, `environment: github-pages` mit `page_url`-Output. Action-Majors aktuell: `checkout@v7` (Repo-Konvention), `configure-pages@v6`, `upload-pages-artifact@v5`, `deploy-pages@v5`. Der bekannte `actions: read`-Permission-Fallstrick von `deploy-pages@v4.0.0` ist in v5 behoben (Artifacts-Client intern) — die Permissions sind ausreichend.

**Doku** — `docs/GITHUB_PAGE.md` vollständig (Aufbau, Pflege, Screenshot-Verfahren inkl. korrekter Port- 5039- und Routen-Angaben — `MediaSourceDetails`/`MovieCollectionDetails`/`TVShowDetails`/`LiveTv` existieren, Aktivierung + manueller Fallback). Verlinkungen in `README.md`, `docs/INDEX.md`, `docs/STAGING_WORKFLOW.md`, `docs/RELEASE_NOTES.md` (EN+DE, „infrastructure only") korrekt.

**Repo-Checks selbst ausgeführt** — `dotnet run --project tools/MarkdownLinkCheck -- --root . --solution VideoPlayer.sln`: **bestanden (477 Dateien, 342 lokale Links)**. Working tree sauber; `steps.md` korrekt auf „In Arbeit".

**Einschränkungen/Anmerkungen (keine Abweichungen):**

- Live-Erreichbarkeit von `https://martin-stromberg.github.io/VideoPlayer/` ist erst nach Erreichen von `main` nachweisbar (planmäßiger Scope-Ausschluss); `configure-pages` aktiviert Pages automatisch, dokumentierter Fallback vorhanden.
- `pages/assets/img/logo.webp` (41 KB) wird nirgends referenziert — ungenutztes Asset, kosmetisch.
- Der `paths`-Filter erfasst `pages.yml` selbst nicht; ein Workflow-Fix allein löst keinen Push-Lauf aus (`workflow_dispatch` deckt das ab).
- Kleinigkeit im App-Screenshot `medienquelle.webp`: Poster-Untertitel der Karten überlappen sich leicht (App-Darstellung, kein Mangel der Seite).
