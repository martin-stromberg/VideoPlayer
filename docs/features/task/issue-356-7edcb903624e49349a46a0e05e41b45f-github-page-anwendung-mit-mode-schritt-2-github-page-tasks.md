# Tasks: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment (Schritt 2, Issue #356)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Seite | `pages/index.html` mit Anker-Abschnitten (Hero/Zweck, Features, Screenshots, Download, Links) auf Deutsch anlegen | Offen | — |
| 2 | Seite | `pages/assets/css/site.css` mit dunkler (`--vp-*`) und abgeleiteter heller Palette, responsivem Layout anlegen | Offen | — |
| 3 | Seite | `pages/assets/js/theme.js` + Inline-Bootstrap (`prefers-color-scheme` + `localStorage`-Toggle) anlegen | Offen | — |
| 4 | Assets | `favicon.png` nach `pages/assets/img/` kopieren; Logo aus `Images/app.png` verkleinern (`logo.webp`/`logo.png`) | Offen | — |
| 5 | Screenshots | Demoinstallation lokal starten, Erstregistrierung mit `Demobibliothek`, Screenshots aufnehmen | Offen | — |
| 6 | Screenshots | Screenshots optimiert (WebP) unter `pages/assets/screenshots/` ablegen und in der Galerie verdrahten | Offen | — |
| 7 | Deployment | `.github/workflows/pages.yml` (Trigger `push`→`main` + `workflow_dispatch`, Jobs `build`/`deploy`, Konventionen) anlegen | Offen | — |
| 8 | Dokumentation | `docs/GITHUB_PAGE.md` (Pflege, Screenshot-Verfahren, Deployment inkl. Aktivierung/Fallback) erstellen | Offen | — |
| 9 | Dokumentation | `docs/INDEX.md` um GitHub-Page-Eintrag ergänzen | Offen | — |
| 10 | Dokumentation | `README.md` um Passus zur Präsentationsseite ergänzen | Offen | — |
| 11 | Dokumentation | `docs/STAGING_WORKFLOW.md` um `pages.yml` in „CI Workflow Files" ergänzen | Offen | — |
| 12 | Dokumentation | `docs/RELEASE_NOTES.md` um Infrastruktur-Eintrag (EN/DE, Issue #356) ergänzen | Offen | — |
| 13 | Abnahme | Lokale E2E-Abnahme der Seite per Playwright (Inhalte, Theme-Toggle + Persistenz, 375px) inkl. Screenshots und Sichtprüfung | Offen | — |
| 14 | Abnahme | DoD-Nachweis: `dotnet build` Debug + Release, volle Testsuite, Pre-Commit-Checks | Offen | — |
