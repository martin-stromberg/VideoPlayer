# Tasks: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Bestand | `demo-video.mp4` (h264, faststart, ~3 s, klein) erzeugen und unter `VideoWebPlayer/DemoDataSets/Demobibliothek/` beilegen | Fertig | `demo-video.mp4` beigelegt (h264, 640x360, ~3 s, ~134 KB, ffprobe-verifiziert) |
|| 2 | Bestand | `Demobibliothek.json`-Definition anlegen (Name, Beschreibung, mediaSources mit `sourceType`/`contentDirectory`/`videoFile`/`path`) | Fertig | `GetAvailableAsync_ReturnsDemobibliothek` gruen |
|| 3 | Bestand | Medien-Template-Baum anlegen: 3 Filme (`.nfo` mit `<movie>`), 2 Serien (`tvshow.nfo` + `Staffel 1/` mit 3 `<episodedetails>`-NFOs) | Fertig | `ApplyAsync_MaterializesMediaDirectory` gruen (9 MP4/11 NFO/20 JPG) |
|| 4 | Logik | `DemoMediaImageGenerator` implementieren (SkiaSharp-JPEGs: Poster 500×750, Fanart 1280×720, Banner 1000×185, Episode 640×360) | Fertig | `Create_ReturnsDecodableJpeg` gruen (Poster 800x1200, Fanart 1280x720, Banner 1000x185, Episode 640x360) |
|| 5 | Logik | `DemoMediaContentMaterializer` implementieren (Baum kopieren, `.mp4` je NFO, Bilder schreiben, idempotent) | Fertig | `ApplyAsync_MaterializesMediaDirectory` gruen |
|| 6 | Logik | `DemoMediaSourceDefinition` um `SourceType`/`ContentDirectory`/`VideoFile` erweitern | Fertig | Build+Tests gruen |
|| 7 | Logik | `ApplyAsync`: Materialisierung, `SourceType` setzen, `path` relativ auflösen | Fertig | `ApplyAsync_CreatesLocalDirectorySourceWithAccess` gruen |
|| 8 | Logik | `ApplyAsync`: Scan nach Commit via `MediaSourceScanCoordinator.RunFullScanAsync` anstoßen | Fertig | `ApplyAsync_LibraryIsFilledAfterScan` + `RegisterFirstUser_WithDemoDataSet_FillsLibrary` gruen |
|| 9 | Logik | DI: `DemoMediaContentMaterializer`/`DemoMediaImageGenerator` registrieren bzw. injizieren | Fertig | DI in `ServiceCollectionExtensions.cs`, Build gruen |
|| 10 | Validierung | `sourceType`-Parsing mit Fehlerfall, `contentDirectory`/`videoFile`-Prüfung | Fertig | `ParseSourceType`/`RequireVideoFile`/`ResolveDemoDataPath` mit InvalidOperationException |
|| 11 | Build | csproj: `DemoDataSets\**` in Output/Publish kopieren; Publish-Output verifizieren | Fertig | `Content Update`/`None Update` in csproj; Dateien in `bin` Debug+Release verifiziert |
|| 12 | Build | `.gitignore`: `/VideoWebPlayer/Data/DemoMedia/` | Fertig | `.gitignore` Eintrag gesetzt |
|| 13 | Tests | `FileSystemDemoDataSetServiceE2ETests` (Quelltyp, Verzeichnis, Bibliothek, GetAvailableAsync) | Fertig | 5/5 Tests gruen |
|| 14 | Tests | `DemoMediaImageGeneratorTests` (dekodierbares JPEG, Maße) | Fertig | 4/4 Tests gruen |
|| 15 | Tests | `DemoDataRegistrationE2ETests` (Auswahlfeld sichtbar, Happy Path gefüllte Bibliothek, ohne Auswahl leer) | Fertig | 3/3 Tests gruen |
|| 16 | Dokumentation | `docs/help/` Demoinstallation + `docs/help/index.md` | Fertig | `docs/help/einrichtung.md` Abschnitt Demodatenbestand |
|| 17 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` aktualisieren | Fertig | README + RELEASE_NOTES (EN/DE) aktualisiert |

## Ergebnis (2026-11-23)

- Builds Debug+Release: 0 Fehler. Vollsuite (Release): 2983 bestanden, 0 Fehler, 2 uebersprungen.
- Reale Abnahme `dotnet run` + Playwright: Erstregistrierung mit Demobibliothek -> RegisterConfirmation,
  Home/Bibliothek gefuellt, Film-/Serien-Details mit Artwork, Episoden-Stream HTTP 200 (`ftypisom...avc1`).
- Screenshots unter Feature-Verzeichnis `screenshots/` einzeln sichtgeprueft: keine Defekte.
- Bekannter Seiteneffekt: E2E-Dispose raeumt `Data/DemoMedia` unter dem Projekt-ContentRoot ab;
  waehrend eines parallelen `dotnet run` fuehrt das zu 404 auf Streams (dokumentiert, kein Produktbefund).
- Debug-Suite zeigt bei 7 bestehenden Playwright-Tests `404 /_framework/dotnet-browser-tools/hot-reload-settings.json`
  (nur unter `dotnet watch` ausgeliefert; Debug-Artefakt, unabhaengig von dieser Aenderung; Release-Lauf gruen).
