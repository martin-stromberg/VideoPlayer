# Code-Review – Schritt 1 „Demoinstallation"

Geprüfter Diff: Arbeitsstand gegen Projekt-Branch
`task/issue-356-…-github-page-anwendung-mit-mode`. Diese Umgebung kann keine
Unteragenten starten — das Review wurde vom Orchestrations-Agenten gegen den
tatsächlichen Code durchgeführt (dokumentierte Abweichung).

## Ergebnis

**Status:** Keine Befunde

(Drei Befunde aus der eigenen Sichtung wurden direkt behoben und sind unten
dokumentiert; danach liegt kein offener Befund vor.)

## Geprüfte Dateien

- `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs` (geändert)
- `VideoWebPlayer/Services/DemoData/DemoMediaContentMaterializer.cs` (neu)
- `VideoWebPlayer/Services/DemoData/DemoMediaImageGenerator.cs` (neu)
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (geändert)
- `VideoWebPlayer/VideoWebPlayer.csproj` (geändert)
- `.gitignore` (geändert)
- `VideoWebPlayer/DemoDataSets/**` (neu: JSON, NFO-Template, `demo-video.mp4`)
- `VideoWebPlayer.Tests/DemoMediaImageGeneratorTests.cs` (neu)
- `VideoWebPlayer.Tests/FileSystemDemoDataSetServiceE2ETests.cs` (neu)
- `VideoWebPlayer.Tests/DemoDataRegistrationE2ETests.cs` (neu)

## Befunde aus der Sichtung (direkt behoben)

1. **`ResolveDemoDataPath`: Pfad-Präfixprüfung ohne Trenner**
   (`FileSystemDemoDataSetService.cs`) — `StartsWith` auf den Normalpfad hätte
   Geschwisterpfade wie `DemoDataSetsX` akzeptiert. Behoben: Vergleich gegen
   `normalizedRoot + DirectorySeparatorChar`.

2. **`MaterializeAsync`: leeres, aber existierendes Zielverzeichnis**
   (`DemoMediaContentMaterializer.cs`) — ein leer angelegtes Ziel (z. B. nach
   einem Abbruch) wurde als „bereits materialisiert" übersprungen und die
   Bibliothek blieb leer. Behoben: Überspringen nur bei nicht-leerem Ziel.

3. **`PaletteFor`: `Math.Abs(int.MinValue)` bleibt negativ**
   (`DemoMediaImageGenerator.cs`) — für Titel mit Hash `int.MinValue` wäre der
   Farbton negativ an `SKColor.FromHsl` gegangen. Behoben: `hash & int.MaxValue`.

## Einzelfragen der Sichtung

- **Scan-Anstoß nach dem Commit:** `RunFullScanAsync` läuft nach dem erfolgreichen
  Transaktions-Commit und teilt die Fehlerkonvention (`Exception` → Formularfehler,
  User bleibt). Ein fehlschlagender Scan hinterlässt Quelle + Zuordnung — deckt sich
  mit dem geforderten nicht-atomaren Fehlerverhalten (AK-3/AK-4).
- **Scoped-Lifetime:** `FileSystemDemoDataSetService` (scoped) injiziert
  `MediaSourceScanCoordinator` (scoped) — gleiche Lifetime, kein Scope-Hijack,
  kein `IServiceScopeFactory` nötig.
- **Transaktion:** Datei-IO (Materialisierung) läuft innerhalb des
  `BeginTransactionAsync`-Blocks, gehört aber nicht zur Transaktion; bei einem
  Fehler rollt nur die DB-Seite zurück — bewusst so belassen (Zielordner bleibt
  dann ggf. liegen, idempotent wiederverwendbar).
- **`source.Port = 0`/`Host = ""` bei LocalDirectory:** entspricht dem Verhalten
  der Admin-Oberfläche beim Umschalten auf `LocalDirectory`.
- **`Enum.TryParse` für `sourceType`:** unbekannte Werte werfen
  `InvalidOperationException` statt still auf `Sftp` zu fallen — für ein
  repository-versioniertes Format der sichere Weg.
- **Videodatei-Verweis `videoFile` ist Pflicht bei `contentDirectory`** —
  verhindert stille leere Medienbäume.
- **Materialized-Ziel unter `ContentRootPath`:** wird im Repo-Klon über
  `.gitignore` abgedeckt (`/VideoWebPlayer/Data/DemoMedia/`).
- **UI:** keine Razor-/CSS-/JS-Änderung; `Register.razor` unverändert, bestehende
  Auswahl bleibt wie gefordert bestehen.
- **Keine Schemaänderung:** keine neuen Tabellen/Spalten → keine Migration,
  keine Backup-Tabellenpflicht.
- **Test-Seiteneffekt (dokumentiert, kein Produktbefund):** die neuen E2E-Tests
  räumen `Data/DemoMedia` unter dem Projekt-ContentRoot in `Dispose` ab; läuft
  zeitgleich ein `dotnet run` aus demselben Verzeichnis, verlieren dessen
  Medienitems ihre Dateien (Streaming 404). In CI/Einzelbetrieb unkritisch;
  für den lokalen Vollauf-Lauf zu beachten.

## Verbliebene Risiken

- `Data/DemoMedia` unter `ContentRootPath` ist im produktiven Betrieb nur dann
  beschreibbar, wenn das Server-Konto Schreibrechte hat — Fehler laufen über den
  bekannten Weg ins Registrierungsformular. Mitgelieferter Bestand ist damit
  konsistent zum übrigen Verhalten (z. B. `Data/WebVideoPlayer.db`).
