# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### LiveStreamStandbySegmenter.cs (LiveStreamStandbySegmenter)

- **Toter Code** — `using System.Globalization;` (Zeile 4) wird in der Datei nicht verwendet (`CultureInfo` kommt nirgends vor — derselbe Befundtyp wie der in Runde 1 bereinigte `CodeAnalysis`-Using im Provider).
  Empfehlung: Using entfernen.

- **Fehlerbehandlung / Logik** — Das Löschen der Alt-Segmente (Zeilen 111–115) schluckt `IOException`/`UnauthorizedAccessException` lautlos; Zeile 186 sammelt anschließend per `Directory.GetFiles(cacheDirectory, "standby_*.ts")` alle Dateien — auch ein nicht löschbares Altsegment (z. B. `standby_007.ts` einer früheren, längeren Transkodierung) wandert damit unbemerkt in den neuen Satz und wird in der Playlist beworben.
  Empfehlung: Delete-Fehler mindestens loggen (LogWarning); besser die aufgenommenen Dateien auf die tatsächlich produzierten beschränken (z. B. Löschfehler als Abbruch werten oder nur seit Laufbeginn neu geschriebene Dateien einsammeln).

- **Fehlerbehandlung / Timeout** — `stderrTail = await stderrTask` (Zeile 160) wartet unbegrenzt. Schlägt `process.Kill()` im Timeout-Pfad fehl (der Catch in Zeile 156 schluckt das ohne Log), läuft ffmpeg weiter und `TranscodeAsync` hängt wieder endlos — genau der Blockierpfad, den `TranscodeTimeout` beseitigen sollte (und der den `RefreshAsync`-Await des Workers lähmt).
  Empfehlung: `stderrTask` im `timedOut`-Pfad ebenfalls begrenzen (z. B. `stderrTask.WaitAsync(...)` analog `PumpDrainTimeout`) und einen Kill-Fehler loggen.

### SegmenterProcessSupport.cs (SegmenterProcessSupport)

- **Fehlerbehandlung** — `StartRealProcess` fängt `catch (Exception) { return null; }` (Zeilen 47–50): Die Startursache (z. B. `Win32Exception` „nicht gefunden" vs. „Zugriff verweigert") geht verloren; beide Aufrufer protokollieren nur die kontextlose Meldung „Der ffmpeg-Prozess konnte nicht gestartet werden.".
  Empfehlung: Logger-Parameter ergänzen und `ex` mitloggen oder die Exception an den Aufrufer weiterreichen (z. B. über ein Rückgabe-Outcome), damit die Fehlermeldung die Ursache enthält.

### LiveStreamStandbyProvider.cs (LiveStreamStandbyProvider)

- **Toter Code** — `StandbyCacheMarker.PreparedAtUtc` (Zeile 490, geschrieben in Zeile 409) wird persistiert, aber nie gelesen — dasselbe Muster, das Runde 1 an `Source`/`SourceWriteTimeUtc` beanstandet hat (die sind inzwischen in `CacheMarkerMatchesInput` genutzt; `PreparedAtUtc` bleibt schreib-only). Zusätzlich wird der Stempel mit `DateTime.UtcNow` statt dem injizierten `_timeProvider` gesetzt.
  Empfehlung: Feld aus `StandbyCacheMarker` entfernen (oder fachlich nutzen, z. B. Cache-Alter loggen) — bei Behalten `_timeProvider` verwenden.

### LiveStreamController.cs (LiveStreamController)

- **Fehlerbehandlung / toter Code** — `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` (Zeile 120): Der `UnauthorizedAccessException`-Filter ist unerreichbar, weil der Catch in Zeile 115 den Typ bereits vorher abfängt. Konsequenz: Eine echte Datei-Zugriffsverweigerung beim Ausliefern (z. B. FileStream auf das Segment-Verzeichnis) wird als `401 Unauthorized` mit `ex.Message` an den Client gemeldet statt als 500 — die 401-Behandlung war offenbar nur für `CheckLoggedIn` gedacht.
  Empfehlung: Den `or UnauthorizedAccessException`-Teil aus dem Filter entfernen und die 401-Behandlung auf den Anmeldefehler beschränken (z. B. `CheckLoggedIn()` in einen eigenen try/catch oder einen dedizierten Exception-Typ), damit I/O-Zugriffsfehler als 500 geloggt werden.

### FfmpegLiveStreamSegmenterE2ETests.cs (Doppelter Code — Restbestand aus Runde 1)

- **Doppelter Code** — Die Konsolidierung auf `E2EBinaryLocator` ist unvollständig: Die private `FindBinary`-Kopie (Zeile 200, Aufrufe in Zeilen 21 und 77) besteht weiterhin. Zwar wurde die Datei in diesem Branch nicht angefasst, aber genau sie war der in Runde 1 benannte dritte Kopien-Standort.
  Empfehlung: `FindBinary` löschen und an beiden Aufrufstellen `E2EBinaryLocator.Find("ffmpeg")` verwenden (using `VideoWebPlayer.Tests.Helpers` ergänzen).

### LiveTvE2ETests.cs

- **Struktur / Dokumentation** — Zeilen 41–46: Der `<summary>`-Block von `SeedChannelWithRunningProgramAsync` („…plus the HLS segment directory so the player actually reaches live.m3u8") hängt jetzt oberhalb von `SeedChannelWithRunningProgram_NoPlaylistAsync` — es stehen zwei `<summary>`-Elemente vor der falschen Methode, und `SeedChannelWithRunningProgramAsync` hat gar keinen Doc-Kommentar mehr.
  Empfehlung: Zeilen 41–44 zurück über `SeedChannelWithRunningProgramAsync` (vor Zeile 55) verschieben.

## Geprüfte Dateien

- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` (neu)
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs` (neu)
- `VideoWebPlayer/Services/SegmenterProcessSupport.cs` (neu)
- `VideoWebPlayer/Services/MediaItemClipEligibility.cs` (neu)
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
- `VideoWebPlayer/Controllers/LiveStreamController.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/ProgramClipGroupService.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/LiveTv.razor`
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Migrations/20261026154346_AddSetupLiveStreamStandbyMediaItem.cs` + `…Designer.cs` (neu)
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`, `.gitignore`, `VideoWebPlayer/Assets/LiveStreamStandby/*.ts` (neu, eingebettete Ressourcen)
- `VideoWebPlayer.Tests/Helpers/E2EBinaryLocator.cs` (neu)
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` (nur Befund-Prüfung, unverändert im Branch)
- `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`
- `VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`
- `VideoWebPlayer.Tests/LiveStreamE2ETests.cs`
- `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
- `VideoWebPlayer.Tests/LiveTvE2ETests.cs`
- `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs`
- `VideoWebPlayer.Tests/SetupsSingleRowMigrationTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_Standby.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs` (neu)
- `docs/…/generate_clips.py` (unter `docs/` — übersprungen)
