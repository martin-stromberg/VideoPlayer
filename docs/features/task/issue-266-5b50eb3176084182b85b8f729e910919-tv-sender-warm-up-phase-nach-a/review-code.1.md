# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### LiveStreamStandbySegmenter.cs (LiveStreamStandbySegmenter)

- **Fehlerbehandlung** — `TranscodeAsync` wartet in Zeile 128 mit `process.WaitForExitAsync(cancellationToken)` unbegrenzt auf den ffmpeg-Prozess. Das `-t 60`-Argument (Zeile 188) deckelt nur die Ausgabedauer, nicht die Laufzeit: Ein stallender Input (hängender HTTP-Read, nie endender Pipe-Stream) lässt ffmpeg unendlich laufen. `ProbeDurationAsync` in derselben Datei hat dagegen einen 10-Sekunden-Timeout (Zeile 260) — inkonsistent. Konkret kritisch, weil `LiveStreamWorker.ExecuteAsync` `RefreshAsync` vor der Startschleife `await`et: Ein hängender Transcode verhindert, dass die Worker-Hauptschleife je startet — dann läuft gar kein Live-Stream.

  Empfehlung: Timeout um `WaitForExitAsync` legen (z. B. `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter`, Muster wie `ProbeDurationAsync` Zeilen 259–273) mit `Kill()` im Timeout-Fall und Rückgabe `null`.

- **Doppelter Code** — `DrainStandardErrorAsync` (Zeilen 191–211), `PumpInputAsync` (Zeilen 213–237), `StartRealProcess` (Zeilen 290–300), die Input-Spec-Erzeugung `FilePath ?? HttpUrl ?? "pipe:0"` inkl. `-nostdin`-Logik (Zeilen 175–181) und der Codec-Argumentblock `-map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -c:a aac -b:a 128k -force_key_frames "expr:gte(t,n_forced*N)"` (Zeilen 184–187) sind nahezu wörtlich aus `FfmpegLiveStreamSegmenter` (dort Zeilen 246–267, 331–360, 376–396, 399–409) kopiert; `ISegmenterProcess`/`SegmenterProcess` werden bereits geteilt, die Hilfslogik aber nicht. Zusätzlich existiert `NominalSegmentDuration` (4 s) identisch in `LiveStreamStandbySegmenter` (Zeile 42) und `LiveStreamStandbyProvider` (Zeile 64).

  Empfehlung: Gemeinsame Helfer extrahieren (z. B. statischer `SegmenterProcessSupport` für Drain/Pump/StartRealProcess und eine `LiveStreamInput`-Methode oder Shared-Builder für Input-Spec + `-nostdin`); die Codec-Argumente in eine gemeinsame Konstante/Methode überführen; `NominalSegmentDuration` nur an einer Stelle definieren.

### LiveStreamStandbyProvider.cs (LiveStreamStandbyProvider)

- **Fehlerbehandlung** — `PrepareAsync` setzt `_failedMediaItemId` nur auf den kontrollierten `return`-Pfaden (Zeilen 235, 245, 253). Exceptions propagieren dagegen unmarkiert: ungefilterte `File.ReadAllBytesAsync`-Fehler in `TryLoadFromCacheAsync` (Zeile 332) und `LoadSetFromDirectoryAsync` (Zeile 357), eine `NullReferenceException` bei `"files": null` im Cache-Marker (`marker.Files.Count`, Zeile 322) oder eine werfende `_transcodeAsync` lassen `_failedMediaItemId` ungesetzt. Da `BuildPlaylist` bei `configuredMediaItemId != active.MediaItemId` pro Request erneut `TriggerPrepare` auslöst, läuft bei einem persistent werfenden Fehler bei jedem Playlist-Poll (hls.js: alle paar Sekunden) ein kompletter Vorbereitungslauf inkl. ffmpeg — genau der Retry-Sturm, den der Dokumentkommentar (Zeile 205–206) ausdrücklich ausschließt.

  Empfehlung: In `PrepareAsync` einen `catch (Exception)` (mit `OperationCanceledException`-Re-Throw) ergänzen, der loggt und `_failedMediaItemId = target` setzt; `marker.Files` null-sicher prüfen (`marker.Files is { Count: > 0 }` statt `marker.Files.Count == 0`). Optional in `BuildPlaylist`/`TriggerPrepare` die Failed-ID prüfen, bevor ein Task gestartet wird.

- **Toter Code / fehlende Cache-Invalidierung** — `WriteCacheMarker` (Zeilen 365–387) persistiert `Source` und `SourceWriteTimeUtc`, `TryLoadFromCacheAsync` (Zeilen 304–345) liest beide Felder nie: Validierung erfolgt nur über `MediaItemId` + Dateiexistenz. Wird die Quelldatei desselben MediaItems ersetzt, liefert der Cache dauerhaft den alten Clip; `SourceWriteTimeUtc` suggeriert eine Invalidierung, die nicht existiert.

  Empfehlung: Entweder beim Cache-Load `SourceWriteTimeUtc` gegen das aktuelle `File.GetLastWriteTimeUtc` der Quelldatei validieren (und bei Abweichung neu transkodieren) oder die ungenutzten Felder aus `StandbyCacheMarker` entfernen.

- **Fehlerbehandlung / Logik** — `PrepareAsync` prüft den Cache (Zeile 228, `TryLoadFromCacheAsync`) vor der MediaItem-Auflösung (Zeile 231): Ist das konfigurierte MediaItem gelöscht, aber ein Cache-Marker vorhanden, wird der veraltete Clip unbegrenzt weiter ausgeliefert, statt wie im `Setup.cs`-Kommentar (Zeilen 123–127: „deleting the media item does not reset the setting — the provider falls back to the default") dokumentiert auf den Standard zurückzufallen — die Resolver-Existenzprüfung wird nie erreicht.

  Empfehlung: Vor dem Cache-Hit die Existenz des MediaItems prüfen (z. B. `_resolveMediaInputAsync` zuerst aufrufen und den Cache nur bei erfolgreicher Auflösung nutzen) oder die Doku an das tatsächliche Cache-Verhalten anpassen.

- **Toter Code** — `using System.Diagnostics.CodeAnalysis;` (Zeile 3) wird in der Datei nicht verwendet.

  Empfehlung: Using entfernen.

### LiveStreamController.cs (LiveStreamController)

- **Struktur** — `GetFile` (Zeilen 60–148) ist mit ~90 Zeilen deutlich über der ~50-Zeilen-Grenze und enthält jetzt drei orthogonalen Auslieferungspfade (Disk-Playlist mit Discontinuity-Logik, Disk-Segment, Standby-Fallback für Playlist + In-Memory-Segment).

  Empfehlung: Den Standby-Zweig (Zeilen 111–131) in eine eigene Methode auslagern, z. B. `ServeStandbyAsync(string fileName, string? accessToken)`; optional auch den Playlist-Zweig (Zeilen 78–94) extrahieren.

### LiveTvE2ETests.cs

- **Toter Code / Testqualität** — `standbySegmentStatuses` (Zeile 191) wird im Response-Handler befüllt (Zeile 200), aber in `LiveTv_Warmup_PlaysStandbyWithoutErrorHint` nie ausgewertet — die implizit mitgeschnittene Segment-Auslieferung bleibt ungeprüft.

  Empfehlung: Entweder `Assert.Contains(200, standbySegmentStatuses)` ergänzen (Standby-Segment kommt mit 200, nicht 404) oder die Liste entfernen.

### LiveStreamE2ETests.cs

- **Toter Code** — `var segDir = await SeedSegmentDirectoryAsync(writePlaylist: false);` in `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` (Zeile ~170) weist eine Variable zu, die nicht verwendet wird.

  Empfehlung: `await SeedSegmentDirectoryAsync(writePlaylist: false);` ohne Zuweisung.

- **Doppelter Code** — `FindBinary` (Zeilen 260–273) dupliziert `FindBinaryAsync` aus `MediaProbeHttpIntegrationTests.cs` (Zeilen 155–167) nahezu wörtlich.

  Empfehlung: Helper in eine gemeinsame Test-Basisklasse/-Klasse (z. B. `E2EBinaryLocator`) auslagern und an beiden Stellen verwenden.

### ProgramSettingsService.cs (ProgramSettingsService)

- **Doppelter Code** — `UpdateLiveStreamStandbyClipAsync` (Zeilen ~477–491) reimplementiert die Eignungsprüfung „Videodatei + nicht klassifiziert" (`IsVideoFileName` + `MovieMediaItems`/`TVShowEpisodeMediaItems`-Any) aus `ProgramClipGroupService.AddClipsToGroupAsync` (Zeilen 186–203), obwohl beide Stellen dieselbe fachliche Regel für denselben Kandidatenkreis durchsetzen; bei Regeländerungen driften sie auseinander.

  Empfehlung: Die Prüfung in eine geteilte Methode auslagern (z. B. `ProgramClipGroupService`/`MediaItem`-Hilfsmethode `ValidateUnclassifiedVideoClipAsync(id)`), die beide Aufrufer verwenden.

## Geprüfte Dateien

- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` (neu)
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs` (neu)
- `VideoWebPlayer/Controllers/LiveStreamController.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Migrations/20261026154346_AddSetupLiveStreamStandbyMediaItem.cs` + `…Designer.cs` (neu)
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`, `.gitignore`, `VideoWebPlayer/Assets/LiveStreamStandby/*.ts` (neu, eingebettete Ressourcen)
- `docs/projects/task/issue-111-…/maskottchen/generator/generate_clips.py` (neue Szene `gleich-gehts-los` + `jingle_go` — analog vorhandener Szenen, kein Befund)
- `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`
- `VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`
- `VideoWebPlayer.Tests/LiveStreamE2ETests.cs`
- `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
- `VideoWebPlayer.Tests/LiveTvE2ETests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs`
- `VideoWebPlayer.Tests/SetupsSingleRowMigrationTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_Standby.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs` (neu)
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs` (neu)
