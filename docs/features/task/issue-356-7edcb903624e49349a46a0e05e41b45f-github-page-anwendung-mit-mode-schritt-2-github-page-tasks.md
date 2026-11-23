# Tasks: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment (Schritt 2, Issue #356)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Seite | `pages/index.html` mit Anker-Abschnitten (Hero/Zweck, Features, Screenshots, Download, Links) auf Deutsch anlegen | Erledigt | `test-results.md` |
| 2 | Seite | `pages/assets/css/site.css` mit dunkler (`--vp-*`) und abgeleiteter heller Palette, responsivem Layout anlegen | Erledigt | `test-results.md` |
| 3 | Seite | `pages/assets/js/theme.js` + Inline-Bootstrap (`prefers-color-scheme` + `localStorage`-Toggle) anlegen | Erledigt | `test-results.md` |
| 4 | Assets | `favicon.png` nach `pages/assets/img/` kopieren; Logo aus `Images/app.png` verkleinern (`logo.webp`/`logo.png`) | Erledigt | `test-results.md` |
| 5 | Screenshots | Demoinstallation lokal starten, Erstregistrierung mit `Demobibliothek`, Screenshots aufnehmen | Erledigt | `test-results.md` |
| 6 | Screenshots | Screenshots optimiert (WebP) unter `pages/assets/screenshots/` ablegen und in der Galerie verdrahten | Erledigt | `test-results.md` |
| 7 | Deployment | `.github/workflows/pages.yml` (Trigger `push`→`main` + `workflow_dispatch`, Jobs `build`/`deploy`, Konventionen) anlegen | Erledigt | `test-results.md` |
| 8 | Dokumentation | `docs/GITHUB_PAGE.md` (Pflege, Screenshot-Verfahren, Deployment inkl. Aktivierung/Fallback) erstellen | Erledigt | `test-results.md` |
| 9 | Dokumentation | `docs/INDEX.md` um GitHub-Page-Eintrag ergänzen | Erledigt | `test-results.md` |
| 10 | Dokumentation | `README.md` um Passus zur Präsentationsseite ergänzen | Erledigt | `test-results.md` |
| 11 | Dokumentation | `docs/STAGING_WORKFLOW.md` um `pages.yml` in „CI Workflow Files" ergänzen | Erledigt | `test-results.md` |
| 12 | Dokumentation | `docs/RELEASE_NOTES.md` um Infrastruktur-Eintrag (EN/DE, Issue #356) ergänzen | Erledigt | `test-results.md` |
| 13 | Abnahme | Lokale E2E-Abnahme der Seite per Playwright (Inhalte, Theme-Toggle + Persistenz, 375px) inkl. Screenshots und Sichtprüfung | Erledigt | `test-results.md` |
| 14 | Abnahme | DoD-Nachweis: `dotnet build` Debug + Release, volle Testsuite, Pre-Commit-Checks | Erledigt | `test-results.md` |
