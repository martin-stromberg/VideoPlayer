# Review gegen den Umsetzungsplan – Schritt 1 „Demoinstallation"

Geprüft wird die tatsächliche Umsetzung (Diff gegen Projekt-Branch
`task/issue-356-…-github-page-anwendung-mit-mode` plus unversionierte neue Dateien)
gegen `plan.md` und `requirement.md`. Diese Umgebung kann keine Unteragenten
starten — das Review wurde vom Orchestrations-Agenten gegen den realen Code
durchgeführt (dokumentierte Abweichung).

## Ergebnis

**Status:** Vollständig umgesetzt

## Abgleich Planpunkt für Planpunkt

| Planpunkt | Umsetzung | Nachweis |
|---|---|---|
| `DemoMediaSourceDefinition` um `sourceType`, `contentDirectory`, `videoFile` erweitern | Erledigt: drei neue Properties; `SourceType` wird per `ParseSourceType` (Enum.Parse, Default `Sftp`, unbekannter Wert → `InvalidOperationException`) ausgewertet | `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs:229-242`, `:166-174` |
| `DemoMediaContentMaterializer` (Kopieren, `.mp4` aus Dummy-Video, Bilder, idempotent) | Erledigt: `CopyTree` kopiert den Template-Baum, `PopulateFromNfosAsync` liest jedes `.nfo` und schreibt je Wurzelelement (`movie`, `episodedetails`, `tvshow`) Video- und Bilddateien; vorhandenes Zielverzeichnis wird übersprungen | `VideoWebPlayer/Services/DemoData/DemoMediaContentMaterializer.cs` |
| `DemoMediaImageGenerator` (SkiaSharp, dekodierbare JPEGs) | Erledigt: `CreatePoster` (800×1200), `CreateFanart` (1280×720), `CreateBanner` (1000×185), `CreateEpisodeImage` (640×360), deterministische Palette, Titeltext | `VideoWebPlayer/Services/DemoData/DemoMediaImageGenerator.cs` |
| `ApplyAsync`: `SourceType = LocalDirectory`, absoluter Pfad, `MediaSourceUser`, Scan-Anstoß | Erledigt: Quelle erhält `MediaSourceType.LocalDirectory`, relativer `path` wird unter `ContentRootPath` aufgelöst, Host/Port/Credentials werden für lokale Quellen geleert, `MediaSourceUser`-Zuordnung bleibt, nach dem Commit läuft `MediaSourceScanCoordinator.RunFullScanAsync("Demodaten-Scan")` | `FileSystemDemoDataSetService.cs:99-163` |
| Fehlerverhalten unverändert (User bleibt, Fehler im Formular) | Erledigt: `ApplyAsync` wirft weiter; `Register.razor` unverändert | `FileSystemDemoDataSetService.cs:155-160` |
| Bestand `Demobibliothek` mit NFO-Template-Baum + Dummy-Video | Erledigt: `Demobibliothek.json`, 3 Film-NFOs, 2 `tvshow.nfo`, 6 Episoden-NFOs in `Staffel 1/`, `demo-video.mp4` (h264, 640×360, ~3 s, ~134 KB, ffprobe-verifiziert) | `VideoWebPlayer/DemoDataSets/` |
| Build-/Publish-Übernahme verschachtelter Dateien | Erledigt: `Content Update` für `DemoDataSets\**\*.json`, `None Update` für `DemoDataSets\**`; verifiziert in `bin/Debug` | `VideoWebPlayer/VideoWebPlayer.csproj:87-96` |
| DI-Registrierung | Erledigt: Generator + Materializer als Singletons (zustandslos), Service bleibt scoped | `ServiceCollectionExtensions.cs:344-346` |
| Runtime-Verzeichnis ignorieren | Erledigt: `/VideoWebPlayer/Data/DemoMedia/` in `.gitignore` | `.gitignore:400-401` |
| `Register.razor` unverändert | Erledigt: kein Diff | `git diff` |
| Keine Schemaänderung/EF-Migration | Erledigt: keine neuen Tabellen/Spalten | `git diff` |

## Geplante Tests – Abgleich

| Geplanter Test | Umgesetzt in | Ergebnis |
|---|---|---|
| `GetAvailableAsync_ReturnsDemobibliothek` | `FileSystemDemoDataSetServiceE2ETests` | bestanden |
| `ApplyAsync_CreatesLocalDirectorySourceWithAccess` | `FileSystemDemoDataSetServiceE2ETests` | bestanden |
| `ApplyAsync_MaterializesMediaDirectory` | `FileSystemDemoDataSetServiceE2ETests` | bestanden (9 MP4, 11 NFO, 20 JPG) |
| `ApplyAsync_LibraryIsFilledAfterScan` | `FileSystemDemoDataSetServiceE2ETests` | bestanden (3 Movies, 2 Shows, 2 Seasons, 6 Episoden, Poster gesetzt) |
| `Create_ReturnsDecodableJpeg` | `DemoMediaImageGeneratorTests` (4 Fälle) | bestanden |
| `DemoVideoFile_IsValidMp4Container` | `FileSystemDemoDataSetServiceE2ETests` | bestanden (`ftyp`/`moov`/`trak`/`avc1`) |
| `RegisterPage_FirstUser_ShowsDemoDataSetOption` | `DemoDataRegistrationE2ETests` | bestanden |
| `RegisterFirstUser_WithDemoDataSet_FillsLibrary` | `DemoDataRegistrationE2ETests` | bestanden |
| `RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` | `DemoDataRegistrationE2ETests` | bestanden |

## Abweichungen vom Plan (unwesentlich)

- `GetAvailableAsync_ReturnsDemobibliothek` und `DemoVideoFile_IsValidMp4Container`
  liegen in `FileSystemDemoDataSetServiceE2ETests` statt in einer separaten
  Klasse `FileSystemDemoDataSetServiceTests` — gleiche Abdeckung, eine Klasse weniger.
- Poster-Größe 800×1200 (wie geplant); während der Umsetzung kurzzeitig 500×750,
  im Zuge der Testlauf-Korrektur auf den Planwert angehoben.
- Zusätzlich: `SqliteConnection.ClearAllPools()` im Test-`Dispose`, damit die
  Temp-SQLite-Dateien zuverlässig gelöscht werden können.

## Offene Punkte

Keine. Die tatsächlich ausgelassenen Planbestandteile sind keine: alle
Planungszeilen sind umgesetzt. E2E-Abnahme mit echten Screenshots und die
vollständige Testsuite folgen in Schritt 10 (`test-results.md`).
