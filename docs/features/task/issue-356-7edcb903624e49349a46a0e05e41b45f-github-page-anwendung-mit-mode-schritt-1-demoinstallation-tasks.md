# Tasks: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Bestand | `demo-video.mp4` (h264, faststart, ~3 s, klein) erzeugen und unter `VideoWebPlayer/DemoDataSets/Demobibliothek/` beilegen | Offen | — |
|| 2 | Bestand | `Demobibliothek.json`-Definition anlegen (Name, Beschreibung, mediaSources mit `sourceType`/`contentDirectory`/`videoFile`/`path`) | Offen | — |
|| 3 | Bestand | Medien-Template-Baum anlegen: 3 Filme (`.nfo` mit `<movie>`), 2 Serien (`tvshow.nfo` + `Staffel 1/` mit 3 `<episodedetails>`-NFOs) | Offen | — |
|| 4 | Logik | `DemoMediaImageGenerator` implementieren (SkiaSharp-JPEGs: Poster 500×750, Fanart 1280×720, Banner 1000×185, Episode 640×360) | Offen | — |
|| 5 | Logik | `DemoMediaContentMaterializer` implementieren (Baum kopieren, `.mp4` je NFO, Bilder schreiben, idempotent) | Offen | — |
|| 6 | Logik | `DemoMediaSourceDefinition` um `SourceType`/`ContentDirectory`/`VideoFile` erweitern | Offen | — |
|| 7 | Logik | `ApplyAsync`: Materialisierung, `SourceType` setzen, `path` relativ auflösen | Offen | — |
|| 8 | Logik | `ApplyAsync`: Scan nach Commit via `MediaSourceScanCoordinator.RunFullScanAsync` anstoßen | Offen | — |
|| 9 | Logik | DI: `DemoMediaContentMaterializer`/`DemoMediaImageGenerator` registrieren bzw. injizieren | Offen | — |
|| 10 | Validierung | `sourceType`-Parsing mit Fehlerfall, `contentDirectory`/`videoFile`-Prüfung | Offen | — |
|| 11 | Build | csproj: `DemoDataSets\**` in Output/Publish kopieren; Publish-Output verifizieren | Offen | — |
|| 12 | Build | `.gitignore`: `/VideoWebPlayer/Data/DemoMedia/` | Offen | — |
|| 13 | Tests | `FileSystemDemoDataSetServiceE2ETests` (Quelltyp, Verzeichnis, Bibliothek, GetAvailableAsync) | Offen | — |
|| 14 | Tests | `DemoMediaImageGeneratorTests` (dekodierbares JPEG, Maße) | Offen | — |
|| 15 | Tests | `DemoDataRegistrationE2ETests` (Auswahlfeld sichtbar, Happy Path gefüllte Bibliothek, ohne Auswahl leer) | Offen | — |
|| 16 | Dokumentation | `docs/help/` Demoinstallation + `docs/help/index.md` | Offen | — |
|| 17 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` aktualisieren | Offen | — |
