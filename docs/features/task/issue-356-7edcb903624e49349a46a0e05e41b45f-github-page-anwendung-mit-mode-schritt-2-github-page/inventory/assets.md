# Bestandsaufnahme-Detail: Assets, Demoinstallation und Screenshot-Infrastruktur

## Logo und Favicon

| Datei | Maße | Größe | Eignung für die Seite |
|-------|------|-------|------------------------|
| `Images/app.png` | 1024×1024 | ~1,37 MB | Logo — für Web-Einsatz verkleinern (z. B. 256/512 px, PNG oder WebP) |
| `VideoWebPlayer/wwwroot/favicon.png` | 64×64 | ~5 KB | Favicon — direkt verwendbar |

Da das veröffentlichte Pages-Artefakt nur `pages/` enthält, müssen Kopien unter
`pages/assets/img/` abgelegt werden. Python mit Pillow steht in der Umgebung
bereit (verifiziert: `Image.open` auf beiden Dateien erfolgreich) und kann die
Verkleinerung/Optimierung durchführen.

## Demoinstallation (Schritt 1, gemergt)

- Bestand: `VideoWebPlayer/DemoDataSets/Demobibliothek/` +
  `VideoWebPlayer/DemoDataSets/Demobibliothek.json` (`sourceType:
  LocalDirectory`, `contentDirectory: Demobibliothek/Medien`, `videoFile:
  Demobibliothek/demo-video.mp4`, `path: Data/DemoMedia/Demobibliothek`).
- Inhalt: 3 fiktive Filme („Bienen über Berlin", „Der Sternenfalz", „Nachtzug
  nach Marienfluss"), 2 fiktive Serien („Nebelwerk", „Stadtfunk") à 1 Staffel /
  3 Episoden; NFO-Metadaten; `demo-video.mp4` (h264, ~134 KB, im Browser
  abspielbar — in Schritt 1 verifiziert); Anzeigebilder werden zur Laufzeit per
  SkiaSharp generiert (800×1200 Poster).
- Auslöser: Registrierung des ersten Benutzers, Auswahl im
  `<InputSelect id="demo-data-set">` in `VideoWebPlayer/Components/Account/
  Pages/Register.razor` (nur sichtbar bei `isFirstUser && demoDataSets.Count >
  0`); `ApplyAsync` legt Quelle + `MediaSourceUser` an und startet den Scan
  sofort (`MediaSourceScanCoordinator`).
- `.gitignore` deckt `Data/DemoMedia/` ab (Laufzeit-Output, kein Repo-Inhalt).
- Lokaler Lauf: `dotnet run --project VideoWebPlayer` — Profil `http`,
  `applicationUrl: http://localhost:5039` (`Properties/launchSettings.json`).
  Mit `--no-launch-profile` + `ASPNETCORE_URLS` steuerbar; die App legt ihre
  SQLite-DB unter `Data/` an.
- Registrierungsfluss (SignIn.RequireConfirmedAccount aktiv): `Register` →
  Redirect `RegisterConfirmation` (zeigt in Dev den Confirm-Link „Click here to
  confirm your account") → `ConfirmEmail` → `Login` → `/`.
- Relevante Routen für Screenshots: `/` (Startseite/Bibliothek),
  `/mediasource/{Id}` (Quellen-Detailansicht), `/movie/{Id}` /
  `/moviecollection/{Id}` (Film-/Sammlungs-Details), `/tvshow/{Id}` (Serien-
  Details mit Staffeln/Episoden), `/live-tv`, `/playlists`, `/actors`.

## Screenshot-Infrastruktur

- Playwright ist in der Testsuite etabliert (`Microsoft.Playwright`,
  `IPage.ScreenshotAsync` u. a. in `PairingBootstrapE2ETests.cs`,
  `UpdatesPageE2ETests.cs`); E2E-Tests laufen in der Kategorie
  `OsInterface`/`E2E`.
- In der Umgebung stehen Node v24.15.0 und `npx playwright` 1.65.1 bereit —
  damit können die Galerie-Screenshots per kurzem Node-Skript aufgenommen
  werden, ohne einen neuen Test im Solution-Scope zu bauen (die Screenshots
  sind Site-Assets, keine Testartefakte).
- Aufnahme-Hinweis aus Schritt 1: E2E-Test-Dispose räumt `Data/DemoMedia` ab —
  während einer laufenden `dotnet run`-Instanz keine OsInterface-Tests parallel
  ausführen (sonst 404 auf Demo-Streams).

## Lücken

- Keine Screenshots im Repo — müssen aus der laufenden Demoinstallation
  erzeugt, optimiert und unter `pages/assets/screenshots/` versioniert werden.
