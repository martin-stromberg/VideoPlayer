# Bestandsaufnahme: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment (Schritt 2, Issue #356)

Analysiert wurde der Repository-Stand bezogen auf die Anforderung „statische
Präsentationsseite unter `pages/` mit GitHub-Pages-Deployment" (siehe
[requirement.md](requirement.md)). Da der Schritt keinen Produktcode berührt,
konzentriert sich die Erhebung auf Workflow-Konventionen, App-Styling, Bild-Assets,
Demoinstallation/Screenshot-Infrastruktur und Dokumentationslandschaft.

## Zusammenfassung

- **Kein `pages/`-Ordner und kein Pages-Workflow vorhanden:** Es gibt kein
  `pages/`-Verzeichnis und keine `.github/workflows/pages.yml` — beides ist neu
  anzulegen. Die Workflow-Konventionen sind an zehn bestehenden Workflows
  ablesbar (Header-Kommentar, minimale `permissions`, `concurrency`-Gruppe,
  `timeout-minutes`, `ubuntu-latest`, Node-24-Action-Majors).
- **Offizielle Pages-Actions in Node-24-Majors verfügbar:**
  `actions/configure-pages@v6`, `actions/upload-pages-artifact@v5` (nutzt intern
  `upload-artifact@v7` — passt zur Repo-Linie `actions/upload-artifact@v7`),
  `actions/deploy-pages@v5`. Standard-Referenz: `permissions: pages: write`,
  `id-token: write`, `contents: read`; Environment `github-pages` mit
  `url: ${{ steps.deployment.outputs.page_url }}`; `concurrency: group: "pages"`,
  `cancel-in-progress: false`.
- **GitHub Pages ist aktuell nicht aktiviert** (in der Vorarbeit per `gh api`
  verifiziert: `has_pages: false`). `configure-pages` mit `enablement`
  aktiviert Pages beim ersten Lauf automatisch; ein manueller Fallback
  (Settings → Pages → Source: „GitHub Actions") ist zu dokumentieren.
- **App-Styling ist ausschließlich dunkel:** `VideoWebPlayer/wwwroot/app.css`
  definiert die `--vp-*`-Tokens (Primär `#e50914`, Sekundär `#00a8e8`,
  Hintergrund `#131313` u. a.) sowie den Font-Stack `Inter, "Segoe UI", Roboto,
  Helvetica, Arial, sans-serif` (Fließtext) und `Montserrat, Inter, "Segoe UI",
  sans-serif` (Überschriften). Eine helle Palette existiert nicht und ist abzuleiten.
- **Logo/Favicon vorhanden:** `Images/app.png` (1024×1024, ~1,37 MB — für die
  Seite zu verkleinern) und `VideoWebPlayer/wwwroot/favicon.png` (64×64, ~5 KB —
  direkt verwendbar). Da das Pages-Artefakt nur `pages/` enthält, sind Kopien
  unter `pages/assets/img/` nötig.
- **Demoinstallation aus Schritt 1 ist gemergt und einsatzbereit:**
  `VideoWebPlayer/DemoDataSets/Demobibliothek/` (3 Filme, 2 Serien à 1 Staffel /
  3 Episoden, NFOs, `demo-video.mp4` h264, SkiaSharp-Anzeigebilder). Auswahl bei
  der Erstregistrierung über `#demo-data-set` in `Register.razor`; `ApplyAsync`
  stößt den Scan sofort an — die Bibliothek ist direkt nach der Registrierung
  gefüllt. E2E-Beleg: `DemoDataRegistrationE2ETests`.
- **Screenshot-Werkzeuge vorhanden:** Node v24 + Playwright 1.65.1 stehen in
  der Umgebung bereit; die Testsuite nutzt Playwright (`ScreenshotAsync`).
  Die App läuft per `dotnet run --project VideoWebPlayer` (Profil `http`,
  Port 5039). Registrierungsfluss: `Register` → `RegisterConfirmation` (enthält
  in Dev den Confirm-Link) → `ConfirmEmail` → `Login`. Relevante Routen für
  Screenshots: `/` (Startseite/Bibliothek), `/mediasource/{id}`, `/movie/{id}`
  bzw. `/moviecollection/{id}`, `/tvshow/{id}`, `/live-tv`, `/playlists`,
  `/actors`.
- **Doku-Einstiegspunkte:** `docs/INDEX.md` (Tabellen „Einstieg" und „Technische
  Dokumentation"), `README.md` (Feature-Liste, Schnellstart),
  `docs/STAGING_WORKFLOW.md` (Abschnitt „CI Workflow Files" listet alle
  Workflows), `docs/RELEASE_NOTES.md` (zweisprachig EN/DE, Infrastruktur-Einträge
  unter „Important Notes"/„Wichtige Hinweise" — Konvention aus Issue #354/#368).
- **Test-Ausgangszustand:** Der Schritt ändert keinen Produktcode und keine
  Testkonfiguration — die Ausgangsbasis ist der Schritt-1-Merge-Stand: volle
  Suite Release 2983 bestanden / 2 begründete Übersprünge (2985 gesamt), 0
  Fehler. Nachweis: [tests.md](inventory/tests.md). Die volle Suite wird als
  DoD-Nachweis am Ende des Schritts erneut ausgeführt.

## Details

- [Workflow-Konventionen und Pages-Deployment](inventory/workflows.md)
- [App-Styling (`--vp-*`-Tokens, Fonts)](inventory/app-styling.md)
- [Assets, Demoinstallation und Screenshot-Infrastruktur](inventory/assets.md)
- [Dokumentationslandschaft und Link-Prüfung](inventory/documentation.md)
- [GitHub-Pages-Zustand](inventory/github-pages.md)
- [Tests](inventory/tests.md)
